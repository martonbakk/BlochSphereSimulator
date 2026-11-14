# Drives the running simulator through Windows UI Automation and takes window screenshots.
# Used to produce the screenshots of the documentation in a repeatable way.
# Usage: . .\tools\ui_driver.ps1 ; Invoke-Ui "H" ; Save-Shot "docs\img\x.png"

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
[Win32]::SetProcessDPIAware() | Out-Null

function Get-SimWindow {
    $p = Get-Process BlochSphereSimulator -ErrorAction Stop | Select-Object -First 1
    return [System.Windows.Automation.AutomationElement]::FromHandle($p.MainWindowHandle)
}

function Find-ByName([string]$name, [string]$type = "Button") {
    $win = Get-SimWindow
    $ct = [System.Windows.Automation.ControlType]::$type
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ct)))
    $el = $win.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if (-not $el) { throw "Element not found: $type '$name'" }
    return $el
}

function Invoke-Ui([string]$name, [int]$waitMs = 1300) {
    $el = Find-ByName $name "Button"
    $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds $waitMs
}

function Set-UiValue([int]$editIndex, [string]$value) {
    # editIndex: 0 = theta, 1 = phi, 2 = gate angle (order of the TextBoxes in the window)
    $win = Get-SimWindow
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
    $edits = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    $edit = $edits[$editIndex]
    $edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($value)
    $edit.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
    Start-Sleep -Milliseconds 300
}

function Select-UiItem([string]$name) {
    $el = Find-ByName $name "ListItem"
    $el.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 300
}

function Toggle-Ui([string]$name) {
    $el = Find-ByName $name "CheckBox"
    $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle()
    Start-Sleep -Milliseconds 300
}

function Save-Shot([string]$path) {
    # PrintWindow renders the simulator window itself, so other windows on top can never end up in the image.
    $p = Get-Process BlochSphereSimulator | Select-Object -First 1
    $hwnd = $p.MainWindowHandle
    Start-Sleep -Milliseconds 300
    $r = New-Object Win32+RECT
    [Win32]::GetWindowRect($hwnd, [ref]$r) | Out-Null
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    $full = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($full)
    $hdc = $g.GetHdc()
    [Win32]::PrintWindow($hwnd, $hdc, 2) | Out-Null   # 2 = PW_RENDERFULLCONTENT (needed for WPF/DirectX content)
    $g.ReleaseHdc($hdc); $g.Dispose()
    # Trim the invisible resize borders of Windows 10/11 windows
    $crop = New-Object System.Drawing.Rectangle 8, 0, ($w - 16), ($h - 8)
    $bmp = $full.Clone($crop, $full.PixelFormat)
    $dir = Split-Path $path -Parent
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose(); $full.Dispose()
}

function Scroll-Panel([double]$percent) {
    # Scrolls the right-hand control panel (the only vertically scrollable pane) to the given percent.
    $win = Get-SimWindow
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsScrollPatternAvailableProperty, $true)
    foreach ($el in $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
        $sp = $el.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
        if ($sp.Current.VerticallyScrollable -and $el.Current.BoundingRectangle.Height -gt 500) {
            $sp.SetScrollPercent([System.Windows.Automation.ScrollPattern]::NoScroll, $percent)
            Start-Sleep -Milliseconds 300
            return
        }
    }
}
