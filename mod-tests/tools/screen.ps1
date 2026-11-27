# Screenshot of the primary screen, a click at x,y, or Ctrl+F<n> sent to the game window:
# screen.ps1 shot <file> | click <x> <y> | ctrlf <n>
param([string]$Action, [string]$A, [string]$B)
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Mouse {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, UIntPtr e);
}
"@
[Mouse]::SetProcessDPIAware() | Out-Null
if ($Action -eq "shot") {
  $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
  $bitmap.Save($A, [System.Drawing.Imaging.ImageFormat]::Png)
  "$($bounds.Width)x$($bounds.Height)"
} elseif ($Action -eq "click") {
  [Mouse]::SetCursorPos([int]$A, [int]$B) | Out-Null
  Start-Sleep -Milliseconds 150
  [Mouse]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 80
  [Mouse]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
  "clicked $A,$B"
} elseif ($Action -eq "ctrlf") {
  # Unity reads real key events, so this presses the keys rather than posting messages.
  $game = Get-Process Humankind -ErrorAction Stop | Select-Object -First 1
  [Mouse]::SetForegroundWindow($game.MainWindowHandle) | Out-Null
  Start-Sleep -Milliseconds 400
  $fkey = [byte](0x6F + [int]$A)  # VK_F1 is 0x70
  [Mouse]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
  [Mouse]::keybd_event($fkey, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds 120
  [Mouse]::keybd_event($fkey, 0, 2, [UIntPtr]::Zero)
  [Mouse]::keybd_event(0x11, 0, 2, [UIntPtr]::Zero)
  "sent Ctrl+F$A"
}
