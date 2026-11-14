# Builds the self-contained tutorial page: inlines the cut-out rhino PNGs as data URIs.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$html = [IO.File]::ReadAllText("$here\RhinoGotchi-Tutorial.template.html")
foreach ($n in 'win','standby','lose','final') {
  $b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes("$here\img\$n.png"))
  $html = $html.Replace("__IMG_$($n.ToUpper())__", "data:image/png;base64,$b64")
}
$out = Join-Path (Split-Path -Parent $here) 'RhinoGotchi-Tutorial.html'
[IO.File]::WriteAllText($out, $html, (New-Object Text.UTF8Encoding $false))
"Built $out ($((Get-Item $out).Length) bytes)"
