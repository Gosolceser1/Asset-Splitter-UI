<#
.SYNOPSIS
    RDA File Reader for Anno 117, Anno 1800 (and Anno 2205+)
    
.DESCRIPTION
    Reads RDA archive files (Resource File V2.2 format) used by Anno games.
    Can list files, search for files, and extract file contents.
    Supports zlib compression used in most RDA blocks.
    
.NOTES
    RDA Format Reference: https://github.com/lysanntranvouez/RDAExplorer/wiki/RDA-File-Format
    
    This script is best-effort tooling for quick RDA inspection. It implements a
    subset of the RDA format with narrower assumptions than the production C# parser.
    For full RDA enumeration, extraction, and validation, prefer the C# API:
    src/RDAExplorer/RDAReader.cs + RDAFileExtension.ExtractAll().
    
    RDA V2.2 Format (Anno 1800, Anno 117, Anno 2205):
    - Header: 792 bytes total
      - Magic: 18 bytes UTF-8 "Resource File V2.2"
      - Unknown: 766 bytes (always 0)
      - First block offset: 8 bytes (int64) at position 784
    
    - Block structure (stored as linked list, read from first block forward):
      - File data (variable size per file)
      - File headers (560 bytes each)
      - Optional memory-resident info (16 bytes) if flag set
      - Block header (32 bytes)
    
    - Block header (32 bytes):
      - Flags: 4 bytes (0x01=compressed, 0x02=encrypted, 0x04=memory-resident, 0x08=deleted)
      - NumFiles: 4 bytes
      - CompressedHeaderSize: 8 bytes
      - UncompressedHeaderSize: 8 bytes
      - NextBlockOffset: 8 bytes
    
    - File header (560 bytes):
      - FilePath: 520 bytes (UTF-16, null-terminated)
      - DataOffset: 8 bytes (absolute offset, or relative if memory-resident)
      - CompressedSize: 8 bytes
      - UncompressedSize: 8 bytes
      - Timestamp: 8 bytes
      - Unknown: 8 bytes
    
.EXAMPLE
    # Anno 117 — archives in <game>\maindata\*.rda
    Get-RDAFileList -Path "C:\...\maindata\config.rda"
    Get-RDAFileList -Path "C:\...\maindata\config.rda" -Filter "texts"
    Read-RDAFile -Path "C:\...\maindata\config.rda" -FileName "data/base/config/gui/texts_english.xml"
    Get-RDAInfo -Path "C:\...\maindata\config.rda"

.EXAMPLE
    # Anno 1800 — archives in <game>\maindata\*.rda (data0.rda … data33.rda; higher number = newer patch)
    Get-RDAFileList -Path "C:\...\Anno 1800\maindata\data7.rda"
    Get-RDAFileList -Path "C:\...\Anno 1800\maindata\data7.rda" -Filter "assets"
    Read-RDAFile -Path "C:\...\Anno 1800\maindata\data28.rda" -FileName "data/config/export/main/asset/datasets.xml"
    Get-RDAInfo -Path "C:\...\Anno 1800\maindata\data0.rda"
#>

#region RDA V2.2 Constants

# Header constants
$Script:RDA_MAGIC_V22 = "Resource File V2.2"
$Script:RDA_MAGIC_SIZE = 18
$Script:RDA_HEADER_SIZE = 792
$Script:RDA_FIRST_BLOCK_OFFSET_POS = 784

# Block constants
$Script:RDA_BLOCK_HEADER_SIZE = 32
$Script:RDA_MEMORY_RESIDENT_INFO_SIZE = 16

# File header constants
$Script:RDA_FILE_HEADER_SIZE = 560
$Script:RDA_FILE_PATH_SIZE = 520

# Block flags (bitmask)
$Script:FLAG_COMPRESSED = 0x0001      # Block uses zlib compression
$Script:FLAG_ENCRYPTED = 0x0002       # Block uses encryption (different seed for V2.2)
$Script:FLAG_MEMORY_RESIDENT = 0x0004 # File data is contiguous and compressed together
$Script:FLAG_DELETED = 0x0008         # Block is deleted, skip it

#endregion

#region Helper Functions

function Get-DecryptionSeed {
    <#
    .SYNOPSIS
        Get the decryption seed for an RDA version
    .DESCRIPTION
        RDA encryption uses a Linear Congruential Generator (LCG) seeded with a version-specific value.
        V2.0 (Anno 1404/2070): 0xA2C2A
        V2.2 (Anno 2205/1800/117): 0x71C71C71
    #>
    param(
        [ValidateSet("2.0", "2.2")]
        [string]$Version = "2.2"
    )
    
    switch ($Version) {
        "2.0" { return 0xA2C2A }
        "2.2" { return 0x71C71C71 }
    }
}

function Unprotect-RDAData {
    <#
    .SYNOPSIS
        Unprotect RDA encrypted data using the LCG-based XOR cipher
    .DESCRIPTION
        Unprotects data using the Anno RDA encryption algorithm:
        - Uses Linear Congruential Generator: next = (current * 214013 + 2531011) 
        - XORs each 16-bit word with (seed >> 16) & 0x7FFF
    #>
    param(
        [byte[]]$Data,
        [int]$Seed
    )
    
    if ($Data.Length -eq 0 -or $Seed -eq 0) {
        return $Data
    }
    
    $output = New-Object byte[] $Data.Length
    $current = $Seed
    
    # Process 16-bit words
    for ($i = 0; $i -lt $Data.Length - 1; $i += 2) {
        # LCG step: next = (current * 214013 + 2531011) mod 2^32
        $current = [int](([long]$current * 214013 + 2531011) -band 0xFFFFFFFF)
        
        # Get XOR key from upper bits
        $xorKey = ($current -shr 16) -band 0x7FFF
        
        # Read 16-bit word (little-endian)
        $word = [int]$Data[$i] + ([int]$Data[$i + 1] -shl 8)
        
        # XOR decrypt
        $decrypted = $word -bxor $xorKey
        
        # Write back (little-endian)
        $output[$i] = [byte]($decrypted -band 0xFF)
        $output[$i + 1] = [byte](($decrypted -shr 8) -band 0xFF)
    }
    
    # Handle odd byte at end
    if ($Data.Length % 2 -eq 1) {
        $output[$Data.Length - 1] = $Data[$Data.Length - 1]
    }
    
    return $output
}

function Expand-ZlibData {
    <#
    .SYNOPSIS
        Expand zlib-compressed data
    .DESCRIPTION
        Expands data using zlib/deflate. Handles both raw deflate and zlib-wrapped data.
    #>
    param(
        [byte[]]$CompressedData,
        [int]$UncompressedSize
    )
    
    try {
        # Skip zlib header (2 bytes) if present
        $startOffset = 0
        if ($CompressedData.Length -ge 2) {
            # Check for zlib header (CMF + FLG where CMF=0x78 for deflate)
            if ($CompressedData[0] -eq 0x78) {
                $startOffset = 2
            }
        }
        
        $ms = New-Object System.IO.MemoryStream($CompressedData, $startOffset, $CompressedData.Length - $startOffset)
        $ds = New-Object System.IO.Compression.DeflateStream($ms, [System.IO.Compression.CompressionMode]::Decompress)
        
        $output = New-Object byte[] $UncompressedSize
        $totalRead = 0
        while ($totalRead -lt $UncompressedSize) {
            $read = $ds.Read($output, $totalRead, $UncompressedSize - $totalRead)
            if ($read -eq 0) { break }
            $totalRead += $read
        }
        
        $ds.Close()
        $ms.Close()
        
        return $output
    }
    catch {
        Write-Warning "Decompression failed: $_"
        return $null
    }
}

#endregion

function Get-RDAInfo {
    <#
    .SYNOPSIS
        Get basic information about an RDA file
    .DESCRIPTION
        Returns metadata about an RDA archive including version, size, block count,
        and total file count. Also reports compression/encryption status per block.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path
    )
    
    if (-not (Test-Path $Path)) {
        throw "File not found: $Path"
    }
    
    $fs = [System.IO.File]::OpenRead($Path)
    $reader = New-Object System.IO.BinaryReader($fs)
    
    try {
        # Read magic (18 bytes for V2.2)
        $magicBytes = $reader.ReadBytes($Script:RDA_MAGIC_SIZE)
        $magic = [System.Text.Encoding]::UTF8.GetString($magicBytes)
        
        $version = "Unknown"
        if ($magic -eq $Script:RDA_MAGIC_V22) {
            $version = "2.2"
        }
        elseif ($magic -match "Resource File V2\.0") {
            $version = "2.0"
            Write-Warning "RDA V2.0 (Anno 1404/2070) - some features may not work correctly"
        }
        else {
            Write-Warning "Unexpected magic: $magic"
        }
        
        # Get first block offset
        $fs.Position = $Script:RDA_FIRST_BLOCK_OFFSET_POS
        $firstBlockOffset = $reader.ReadInt64()
        
        # Count blocks and files, track compression status
        $totalFiles = 0
        $totalBlocks = 0
        $compressedBlocks = 0
        $encryptedBlocks = 0
        $memoryResidentBlocks = 0
        $blockOffset = $firstBlockOffset
        
        while ($blockOffset -lt $fs.Length) {
            $fs.Position = $blockOffset
            $flags = $reader.ReadInt32()
            $numFiles = $reader.ReadInt32()
            [void]$reader.ReadInt64()
            [void]$reader.ReadInt64()
            $nextBlockOffset = $reader.ReadInt64()
            
            if (($flags -band $Script:FLAG_DELETED) -eq 0) {
                $totalFiles += $numFiles
                if ($flags -band $Script:FLAG_COMPRESSED) { $compressedBlocks++ }
                if ($flags -band $Script:FLAG_ENCRYPTED) { $encryptedBlocks++ }
                if ($flags -band $Script:FLAG_MEMORY_RESIDENT) { $memoryResidentBlocks++ }
            }
            $totalBlocks++
            
            if ($nextBlockOffset -ge $fs.Length -or $nextBlockOffset -le $blockOffset) {
                break
            }
            $blockOffset = $nextBlockOffset
        }
        
        [PSCustomObject]@{
            Path = $Path
            FileName = [System.IO.Path]::GetFileName($Path)
            FileSizeMB = [math]::Round($fs.Length / 1MB, 2)
            Magic = $magic.Trim()
            Version = $version
            FirstBlockOffset = $firstBlockOffset
            TotalBlocks = $totalBlocks
            TotalFiles = $totalFiles
            CompressedBlocks = $compressedBlocks
            EncryptedBlocks = $encryptedBlocks
            MemoryResidentBlocks = $memoryResidentBlocks
        }
    }
    finally {
        $reader.Close()
        $fs.Close()
    }
}

function Get-RDAFileList {
    <#
    .SYNOPSIS
        List all files in an RDA archive
    .DESCRIPTION
        Enumerates all files in an RDA archive. Supports compressed file headers
        (using zlib). Encrypted blocks cannot be read without the encryption key.
    .PARAMETER Path
        Path to the RDA file
    .PARAMETER Filter
        Optional regex pattern to filter file paths
    .PARAMETER IncludeDetails
        Include file size, offset, and compression details
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path,
        
        [string]$Filter = "",
        
        [switch]$IncludeDetails
    )
    
    if (-not (Test-Path $Path)) {
        throw "File not found: $Path"
    }
    
    $fs = [System.IO.File]::OpenRead($Path)
    $reader = New-Object System.IO.BinaryReader($fs)
    
    try {
        # Get first block offset
        $fs.Position = $Script:RDA_FIRST_BLOCK_OFFSET_POS
        $firstBlockOffset = $reader.ReadInt64()
        
        $blockOffset = $firstBlockOffset
        $blockNum = 0
        $files = [System.Collections.ArrayList]::new()
        
        while ($blockOffset -lt $fs.Length) {
            # Read block header (32 bytes at block offset)
            $fs.Position = $blockOffset
            $flags = $reader.ReadInt32()
            $numFiles = $reader.ReadInt32()
            $compressedHeaderSize = $reader.ReadInt64()
            $uncompressedHeaderSize = $reader.ReadInt64()
            $nextBlockOffset = $reader.ReadInt64()
            
            $isCompressed = ($flags -band $Script:FLAG_COMPRESSED) -ne 0
            $isEncrypted = ($flags -band $Script:FLAG_ENCRYPTED) -ne 0
            $isMemoryResident = ($flags -band $Script:FLAG_MEMORY_RESIDENT) -ne 0
            $isDeleted = ($flags -band $Script:FLAG_DELETED) -ne 0
            
            # Skip deleted blocks
            if ($isDeleted) {
                if ($nextBlockOffset -ge $fs.Length -or $nextBlockOffset -le $blockOffset) { break }
                $blockOffset = $nextBlockOffset
                $blockNum++
                continue
            }
            
            # Calculate file header position
            # File headers are located BEFORE the block header
            # If memory-resident, there's a 16-byte info block between headers and block header
            $memResidentInfoSize = if ($isMemoryResident) { $Script:RDA_MEMORY_RESIDENT_INFO_SIZE } else { 0 }
            $fileHeaderStart = $blockOffset - $memResidentInfoSize - $compressedHeaderSize
            
            # Read file headers (may be compressed and/or encrypted)
            $fs.Position = $fileHeaderStart
            $headerBytes = $reader.ReadBytes([int]$compressedHeaderSize)
            
            # Decrypt if needed
            if ($isEncrypted) {
                $seed = Get-DecryptionSeed -Version "2.2"
                $headerBytes = Unprotect-RDAData -Data $headerBytes -Seed $seed
            }
            
            # Decompress if needed
            if ($isCompressed -and $compressedHeaderSize -ne $uncompressedHeaderSize) {
                $headerBytes = Expand-ZlibData -CompressedData $headerBytes -UncompressedSize ([int]$uncompressedHeaderSize)
                if (-not $headerBytes) {
                    Write-Warning "Block ${blockNum} - Failed to decompress file headers"
                    if ($nextBlockOffset -ge $fs.Length -or $nextBlockOffset -le $blockOffset) { break }
                    $blockOffset = $nextBlockOffset
                    $blockNum++
                    continue
                }
            }
            
            # Parse file headers from the decompressed/raw header data
            $headerStream = New-Object System.IO.MemoryStream(,$headerBytes)
            $headerReader = New-Object System.IO.BinaryReader($headerStream)
            
            try {
                for ($i = 0; $i -lt $numFiles; $i++) {
                    $pathBytes = $headerReader.ReadBytes($Script:RDA_FILE_PATH_SIZE)
                    $filePath = [System.Text.Encoding]::Unicode.GetString($pathBytes).TrimEnd([char]0)
                    $dataOffset = $headerReader.ReadInt64()
                    $compressedSize = $headerReader.ReadInt64()
                    $uncompressedSize = $headerReader.ReadInt64()
                    $timestamp = $headerReader.ReadInt64()
                    [void]$headerReader.ReadInt64()
                    
                    # Apply filter if specified
                    if ($Filter -and $filePath -notmatch $Filter) {
                        continue
                    }
                    
                    if ($IncludeDetails) {
                        [void]$files.Add([PSCustomObject]@{
                            Path = $filePath
                            DataOffset = $dataOffset
                            CompressedSize = $compressedSize
                            UncompressedSize = $uncompressedSize
                            Timestamp = $timestamp
                            Block = $blockNum
                            IsCompressed = ($compressedSize -ne $uncompressedSize)
                            IsMemoryResident = $isMemoryResident
                            BlockFlags = $flags
                        })
                    }
                    else {
                        [void]$files.Add($filePath)
                    }
                }
            }
            finally {
                $headerReader.Close()
                $headerStream.Close()
            }
            
            if ($nextBlockOffset -ge $fs.Length -or $nextBlockOffset -le $blockOffset) {
                break
            }
            $blockOffset = $nextBlockOffset
            $blockNum++
        }
        
        $files
    }
    finally {
        $reader.Close()
        $fs.Close()
    }
}

function Read-RDAFile {
    <#
    .SYNOPSIS
        Read the contents of a file from an RDA archive
    .DESCRIPTION
        Extracts and returns the contents of a single file from an RDA archive.
        Supports zlib-compressed and encrypted files. Memory-resident blocks are 
        handled by decompressing the entire block data first.
    .PARAMETER Path
        Path to the RDA file
    .PARAMETER FileName
        Full path of the file inside the RDA (e.g., "data/base/config/gui/texts_english.xml")
    .PARAMETER AsBytes
        Return raw bytes instead of text
    .PARAMETER Encoding
        Text encoding (default: UTF8)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path,
        
        [Parameter(Mandatory=$true)]
        [string]$FileName,
        
        [switch]$AsBytes,
        
        [System.Text.Encoding]$Encoding = [System.Text.Encoding]::UTF8
    )
    
    # Get file info with block details
    $fileInfo = Get-RDAFileList -Path $Path -Filter "^$([regex]::Escape($FileName))$" -IncludeDetails | 
                Where-Object { $_.Path -eq $FileName } |
                Select-Object -First 1
    
    if (-not $fileInfo) {
        throw "File not found in RDA: $FileName"
    }
    
    $isEncrypted = ($fileInfo.BlockFlags -band $Script:FLAG_ENCRYPTED) -ne 0
    $isBlockCompressed = ($fileInfo.BlockFlags -band $Script:FLAG_COMPRESSED) -ne 0
    
    $fs = [System.IO.File]::OpenRead($Path)
    $reader = New-Object System.IO.BinaryReader($fs)
    
    try {
        # For memory-resident blocks, we need to read and decompress the entire block data
        if ($fileInfo.IsMemoryResident) {
            # Get block info - need to find the block header
            $fs.Position = $Script:RDA_FIRST_BLOCK_OFFSET_POS
            $blockOffset = $reader.ReadInt64()
            
            # Find the right block
            for ($b = 0; $b -lt $fileInfo.Block; $b++) {
                $fs.Position = $blockOffset + 24  # Skip to nextBlockOffset field
                $blockOffset = $reader.ReadInt64()
            }
            
            # Read block header
            $fs.Position = $blockOffset
            [void]$reader.ReadInt32()
            [void]$reader.ReadInt32()
            $compressedHeaderSize = $reader.ReadInt64()
            [void]$reader.ReadInt64()
            [void]$reader.ReadInt64()
            
            # Read memory-resident info (16 bytes before block header)
            $memInfoPos = $blockOffset - $Script:RDA_MEMORY_RESIDENT_INFO_SIZE
            $fs.Position = $memInfoPos
            $mrmCompressedSize = $reader.ReadInt64()
            $mrmUncompressedSize = $reader.ReadInt64()
            
            # Calculate data section start
            $dataStart = $memInfoPos - $compressedHeaderSize - $mrmCompressedSize
            
            # Read entire compressed block data
            $fs.Position = $dataStart
            $blockData = $reader.ReadBytes([int]$mrmCompressedSize)
            
            # Decrypt if needed
            if ($isEncrypted) {
                $seed = Get-DecryptionSeed -Version "2.2"
                $blockData = Unprotect-RDAData -Data $blockData -Seed $seed
            }
            
            # Decompress if needed
            if ($isBlockCompressed) {
                $blockData = Expand-ZlibData -CompressedData $blockData -UncompressedSize ([int]$mrmUncompressedSize)
                if (-not $blockData) {
                    throw "Failed to decompress memory-resident block data"
                }
            }
            
            # Now extract the file from the decompressed block data
            # DataOffset is relative to the start of decompressed data
            $buffer = New-Object byte[] $fileInfo.UncompressedSize
            [Array]::Copy($blockData, [int]$fileInfo.DataOffset, $buffer, 0, [int]$fileInfo.UncompressedSize)
        }
        else {
            # Regular file - read directly from RDA
            $fs.Position = $fileInfo.DataOffset
            $buffer = New-Object byte[] $fileInfo.CompressedSize
            $fs.Read($buffer, 0, [int]$fileInfo.CompressedSize) | Out-Null
            
            # Decrypt if needed (per-file encryption when not memory-resident)
            if ($isEncrypted) {
                $seed = Get-DecryptionSeed -Version "2.2"
                $buffer = Unprotect-RDAData -Data $buffer -Seed $seed
            }
            
            # Decompress if needed
            if ($fileInfo.IsCompressed) {
                $buffer = Expand-ZlibData -CompressedData $buffer -UncompressedSize ([int]$fileInfo.UncompressedSize)
                if (-not $buffer) {
                    throw "Failed to decompress file: $FileName"
                }
            }
        }
        
        if ($AsBytes) {
            $buffer
        }
        else {
            $Encoding.GetString($buffer)
        }
    }
    finally {
        $reader.Close()
        $fs.Close()
    }
}

function Search-RDAContent {
    <#
    .SYNOPSIS
        Search for text content within files in an RDA archive
    .PARAMETER Path
        Path to the RDA file
    .PARAMETER Pattern
        Regex pattern to search for
    .PARAMETER FileFilter
        Optional filter for which files to search (regex on file path)
    .PARAMETER Context
        Number of characters to show before/after match
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path,
        
        [Parameter(Mandatory=$true)]
        [string]$Pattern,
        
        [string]$FileFilter = "\.(xml|txt|lua|json|ini)$",
        
        [int]$Context = 50
    )
    
    $files = Get-RDAFileList -Path $Path -Filter $FileFilter -IncludeDetails
    $results = [System.Collections.ArrayList]::new()
    
    foreach ($file in $files) {
        try {
            $content = Read-RDAFile -Path $Path -FileName $file.Path
            $regexMatches = [regex]::Matches($content, $Pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            
            foreach ($match in $regexMatches) {
                $start = [Math]::Max(0, $match.Index - $Context)
                $length = [Math]::Min($content.Length - $start, $match.Length + $Context * 2)
                $contextText = $content.Substring($start, $length) -replace '[\r\n]+', ' '
                
                [void]$results.Add([PSCustomObject]@{
                    File = $file.Path
                    Match = $match.Value
                    Position = $match.Index
                    Context = $contextText
                })
            }
        }
        catch {
            Write-Verbose "Error reading $($file.Path): $_"
        }
    }
    
    $results
}

function Get-RDABlocks {
    <#
    .SYNOPSIS
        Get detailed information about all blocks in an RDA archive
    .DESCRIPTION
        Returns information about each block including flags, file count,
        compression status, and offsets.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path
    )
    
    if (-not (Test-Path $Path)) {
        throw "File not found: $Path"
    }
    
    $fs = [System.IO.File]::OpenRead($Path)
    $reader = New-Object System.IO.BinaryReader($fs)
    
    try {
        $fs.Position = $Script:RDA_FIRST_BLOCK_OFFSET_POS
        $firstBlockOffset = $reader.ReadInt64()
        
        $blockOffset = $firstBlockOffset
        $blockNum = 0
        $blocks = [System.Collections.ArrayList]::new()
        
        while ($blockOffset -lt $fs.Length) {
            $fs.Position = $blockOffset
            $flags = $reader.ReadInt32()
            $numFiles = $reader.ReadInt32()
            $compressedHeaderSize = $reader.ReadInt64()
            $uncompressedHeaderSize = $reader.ReadInt64()
            $nextBlockOffset = $reader.ReadInt64()
            
            [void]$blocks.Add([PSCustomObject]@{
                BlockNum = $blockNum
                Offset = $blockOffset
                Flags = "0x{0:X4}" -f $flags
                IsCompressed = ($flags -band $Script:FLAG_COMPRESSED) -ne 0
                IsEncrypted = ($flags -band $Script:FLAG_ENCRYPTED) -ne 0
                IsMemoryResident = ($flags -band $Script:FLAG_MEMORY_RESIDENT) -ne 0
                IsDeleted = ($flags -band $Script:FLAG_DELETED) -ne 0
                NumFiles = $numFiles
                CompressedHeaderSize = $compressedHeaderSize
                UncompressedHeaderSize = $uncompressedHeaderSize
                NextBlockOffset = $nextBlockOffset
            })
            
            if ($nextBlockOffset -ge $fs.Length -or $nextBlockOffset -le $blockOffset) {
                break
            }
            $blockOffset = $nextBlockOffset
            $blockNum++
        }
        
        $blocks
    }
    finally {
        $reader.Close()
        $fs.Close()
    }
}

function Get-AllRDAFiles {
    <#
    .SYNOPSIS
        Get info about all RDA files in the game's RDA folder
    .DESCRIPTION
        For Anno 117: RDA files are in <game>\maindata\ (config.rda, shared_configs.rda, ...)
        For Anno 1800: RDA files are in <game>\maindata\ (data0.rda ... data33.rda)
        Pass -MaindataPath to override the folder that is scanned.
    .PARAMETER GamePath
        Game installation directory.
        Anno 117:   C:\Program Files (x86)\Steam\steamapps\common\Anno 117 - Pax Romana
        Anno 1800:  C:\Program Files (x86)\Steam\steamapps\common\Anno 1800
    .PARAMETER MaindataPath
        Override the folder scanned for *.rda files.
        Defaults to <GamePath>\maindata for both Anno 117 and Anno 1800.
    #>
    [CmdletBinding()]
    param(
        [string]$GamePath = "C:\Program Files (x86)\Steam\steamapps\common\Anno 117 - Pax Romana",
        [string]$MaindataPath = ""
    )
    
    $maindata = if ($MaindataPath) { $MaindataPath } else { Join-Path $GamePath "maindata" }
    
    if (-not (Test-Path $maindata)) {
        throw "maindata folder not found: $maindata"
    }
    
    Get-ChildItem $maindata -Filter "*.rda" | ForEach-Object {
        Get-RDAInfo -Path $_.FullName
    } | Sort-Object TotalFiles -Descending
}

# Export functions (only when imported as module)
if ($MyInvocation.MyCommand.ScriptBlock.Module) {
    Export-ModuleMember -Function Get-RDAInfo, Get-RDAFileList, Read-RDAFile, Search-RDAContent, Get-RDABlocks, Get-AllRDAFiles
}

# If running as script (not imported as module), show help
if ($MyInvocation.InvocationName -ne '.') {
    Write-Host @"
╔══════════════════════════════════════════════════════════════════╗
║          RDA File Reader — Anno 117 and Anno 1800                 ║
║                    (Resource File V2.2 Format)                    ║
╠══════════════════════════════════════════════════════════════════╣
║  Usage:                                                           ║
║    . .\Read-RDA.ps1                    # Dot-source to load       ║
║                                                                   ║
║  Commands:                                                        ║
║    Get-RDAInfo -Path <rda>             # Get RDA file info        ║
║    Get-RDABlocks -Path <rda>           # List all blocks          ║
║    Get-RDAFileList -Path <rda>         # List all files           ║
║    Get-RDAFileList -Path <rda> -Filter "pattern"                  ║
║    Read-RDAFile -Path <rda> -FileName "data/..."                  ║
║    Search-RDAContent -Path <rda> -Pattern "text"                  ║
║    Get-AllRDAFiles                     # Anno 117/1800: all maindata RDAs ║
║    Get-AllRDAFiles -MaindataPath $md   # override with explicit path      ║
║                                                                   ║
║  Anno 117  — archives in <game>\maindata\*.rda                   ║
║  Anno 1800 — archives in <game>\maindata\*.rda (data0..data33; higher=newer)║
║                                                                   ║
║  Features:                                                        ║
║    ✓ Reads RDA V2.2 (Anno 1800, Anno 117, Anno 2205)             ║
║    ✓ Zlib decompression for compressed blocks/files              ║
║    ✓ Block-level metadata and flags                              ║
║    ✓ Encryption support (LCG cipher, seed 0x71C71C71)            ║
║    ✓ Memory-resident block extraction                            ║
╚══════════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan
}
