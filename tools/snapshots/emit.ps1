# Prints the pictures of the snapshots as annotations of the run, cut in pieces, so that they can be read through the
# API of GitHub without downloading the artifact. Each step prints at most 10 pieces (the limit of GitHub per step).
param(
    [Parameter(Mandatory = $true)][string]$Folder,
    [Parameter(Mandatory = $true)][int]$Step
)

$size = 4000
$pieces = @()
foreach ($file in Get-ChildItem -LiteralPath $Folder -Filter *.png | Sort-Object Name) {
    $text = [Convert]::ToBase64String([IO.File]::ReadAllBytes($file.FullName))
    $count = [Math]::Ceiling($text.Length / $size)
    for ($index = 0; $index -lt $count; $index++) {
        $length = [Math]::Min($size, $text.Length - ($index * $size))
        $pieces += [pscustomobject]@{ Name = $file.BaseName; Index = $index; Count = $count; Text = $text.Substring($index * $size, $length) }
    }
}

$pieces | Select-Object -Skip ($Step * 10) -First 10 | ForEach-Object {
    Write-Output "::notice title=snap|$($_.Name)|$($_.Index)|$($_.Count)::$($_.Text)"
}

if ($Step -eq 0) {
    Write-Output "Pieces: $($pieces.Count)"
}
