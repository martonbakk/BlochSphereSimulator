# Bloch-gömb szimulátor

Házi feladat – Kvantuminformatikai alkalmazások (BMEVIHIAD00), 2026 ősz, 2. feladat.

WPF (.NET 8) asztali alkalmazás, amely egy vagy több független qubit állapotát mutatja 3D Bloch-gömbön:
13 kapu (X, Y, Z, H, P(α), S, S†, T, T†, √X, Rx, Ry, Rz), animált forgatás, kapulánc visszavonással,
sorszámozott előzmények és pályák, élő állapotkijelző, Z/X-bázisú mérés hisztogrammal, nevezetes kezdőállapotok,
és az előadás állapot-előállító áramköre (H → P(α) → H → P(90°+β)).

## Futtatás

`dist\BlochSphereSimulator.exe` – önálló, telepítést nem igényel (64 bites Windows 10/11).

## Fordítás

```
dotnet build BlochSphereSimulator.sln
dotnet test  BlochSphereSimulator.sln
publish.cmd            # önálló, tömörített single-file .exe a dist\ mappába
```

## Dokumentáció

`docs\BlochSphereSimulator_dokumentacio.docx` és `.pdf`. Újragenerálás: `python tools\build_docs.py`, majd
`powershell -ExecutionPolicy Bypass -File tools\docx_to_pdf.ps1`. A képernyőképeket a futó programon a
`tools\ui_driver.ps1` + `tools\shots_scenario.ps1` szkript készíti.

## Könyvtárak

HelixToolkit.Wpf 3.1.2 (MIT), MathNet.Numerics 5.0.0 (MIT), MSTest 3.6.2 (MIT).
