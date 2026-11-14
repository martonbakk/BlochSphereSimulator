# Screenshot scenario for the documentation. Expects a freshly started simulator.
$ErrorActionPreference = "Stop"
$img = Join-Path $PSScriptRoot "..\docs\img"
if (-not $PSScriptRoot) { $img = "docs\img" }

# 1) Start screen
Save-Shot "$img\01_start.png"

# 2) Preparation demo: target theta=60, phi=120
Set-UiValue 0 "60"
Set-UiValue 1 "120"
Invoke-Ui "▶ Előállítás lépésenként" 6500
Save-Shot "$img\02_preparation_demo.png"

# 3) Mid-animation frame of a Hadamard gate (shows the rotation axis)
Invoke-Ui "|0⟩" 400
Invoke-Ui "H" 450
Save-Shot "$img\03_animation_H.png"
Start-Sleep -Milliseconds 900

# 4) Gate chain with history: |0> -> H -> T -> S -> sqrtX
Invoke-Ui "T"
Invoke-Ui "S"
Invoke-Ui "√X"
Save-Shot "$img\04_chain_history.png"

# 5) Measurement + histogram
Invoke-Ui "Mérés Z-bázisban {|0⟩, |1⟩}"
Invoke-Ui "↶ Visszavonás (Ctrl+Z)" 300
Invoke-Ui "Futtatás" 500
Scroll-Panel 100
Save-Shot "$img\05_shots.png"
Invoke-Ui "Mérés Z-bázisban {|0⟩, |1⟩}"
Save-Shot "$img\05_measurement.png"
Scroll-Panel 0

# 6) Multiple qubits
Invoke-Ui "+ Új qubit" 400
Invoke-Ui "|+⟩" 400
Invoke-Ui "Ry(α)"
Invoke-Ui "+ Új qubit" 400
Invoke-Ui "|−j⟩" 400
Set-UiValue 2 "120"
Invoke-Ui "Rx(α)"
Save-Shot "$img\06_multiple_qubits.png"
"done"
