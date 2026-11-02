Add-Type -AssemblyName System.Drawing
function Rounded([float]$x,[float]$y,[float]$w,[float]$h,[float]$r){$p=[System.Drawing.Drawing2D.GraphicsPath]::new();$p.AddArc($x,$y,$r,$r,180,90);$p.AddArc($x+$w-$r,$y,$r,$r,270,90);$p.AddArc($x+$w-$r,$y+$h-$r,$r,$r,0,90);$p.AddArc($x,$y+$h-$r,$r,$r,90,90);$p.CloseFigure();return $p}
$chunks=@();$sizes=@(16,24,32,48,64,128,256)
foreach($size in $sizes){
 $bitmap=[System.Drawing.Bitmap]::new($size,$size);$g=[System.Drawing.Graphics]::FromImage($bitmap);$g.SmoothingMode='AntiAlias';$g.ScaleTransform($size/256.0,$size/256.0)
 $outer=Rounded 8 8 240 240 58
 $gradient=[System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Point]::new(8,8),[System.Drawing.Point]::new(248,248),[System.Drawing.ColorTranslator]::FromHtml('#216DE3'),[System.Drawing.ColorTranslator]::FromHtml('#7354D9'));$g.FillPath($gradient,$outer)
 $white=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::White);$keyBrush=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#4167CE'))
 $keyboard=Rounded 32 116 145 96 20;$g.FillPath($white,$keyboard)
 foreach($row in @(0,1)){foreach($col in @(0,1,2,3)){$g.FillRectangle($keyBrush,([float](48+$col*29)),([float](132+$row*24)),[float]15,[float]11)}}
 $g.FillRectangle($keyBrush,[float]61,[float]183,[float]83,[float]10)
 $mouse=Rounded 170 45 52 104 50;$g.FillPath($white,$mouse)
 $wheel=[System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#4167CE'),7);$wheel.StartCap='Round';$wheel.EndCap='Round';$g.DrawLine($wheel,196,62,196,80)
 $flow=[System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#9AF0F3'),10);$flow.StartCap='Round';$flow.EndCap='Round';$g.DrawBezier($flow,52,89,76,45,123,40,151,62);$g.DrawLine($flow,151,62,136,48);$g.DrawLine($flow,151,62,133,70)
 $stream=[System.IO.MemoryStream]::new();$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png);$chunks+=,$stream.ToArray();if($size -eq 256){$bitmap.Save((Join-Path $PSScriptRoot 'App.png'),[System.Drawing.Imaging.ImageFormat]::Png)}
 $stream.Dispose();$g.Dispose();$bitmap.Dispose();$outer.Dispose();$keyboard.Dispose();$mouse.Dispose();$gradient.Dispose();$white.Dispose();$keyBrush.Dispose();$wheel.Dispose();$flow.Dispose()
}
$file=[System.IO.File]::Create((Join-Path $PSScriptRoot 'App.ico'));$writer=[System.IO.BinaryWriter]::new($file);$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count
for($i=0;$i -lt $sizes.Count;$i++){$dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$chunks[$i].Length);$writer.Write([uint32]$offset);$offset+=$chunks[$i].Length}
foreach($chunk in $chunks){$writer.Write([byte[]]$chunk)};$writer.Dispose()
