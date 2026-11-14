"""Builds the Hungarian documentation (DOCX) of the Bloch sphere simulator.

Usage:  python tools/build_docs.py
Output: docs/BlochSphereSimulator_dokumentacio.docx  (PDF is exported from it with Word, see tools/docx_to_pdf.ps1)
"""
from pathlib import Path

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor

ROOT = Path(__file__).resolve().parent.parent
IMG = ROOT / "docs" / "img"
OUT = ROOT / "docs" / "BlochSphereSimulator_dokumentacio.docx"

doc = Document()

# ---------- base styles ----------
for section in doc.sections:
    section.top_margin = Cm(2.2)
    section.bottom_margin = Cm(2.2)
    section.left_margin = Cm(2.3)
    section.right_margin = Cm(2.3)

normal = doc.styles["Normal"]
normal.font.name = "Calibri"
normal.font.size = Pt(11)
normal.paragraph_format.space_after = Pt(6)
normal.paragraph_format.line_spacing = 1.15

for level, size in ((1, 16), (2, 13), (3, 11.5)):
    st = doc.styles[f"Heading {level}"]
    st.font.name = "Calibri"
    st.font.size = Pt(size)
    st.font.color.rgb = RGBColor(0x1F, 0x2A, 0x44)


def p(text="", bold_prefix=None, style=None, italic=False, align=None):
    para = doc.add_paragraph(style=style)
    if bold_prefix:
        r = para.add_run(bold_prefix)
        r.bold = True
    r = para.add_run(text)
    r.italic = italic
    if align:
        para.alignment = align
    return para


def bullet(text, bold_prefix=None):
    return p(text, bold_prefix=bold_prefix, style="List Bullet")


def numbered(text, bold_prefix=None):
    return p(text, bold_prefix=bold_prefix, style="List Number")


def formula(text):
    para = doc.add_paragraph()
    para.alignment = WD_ALIGN_PARAGRAPH.CENTER
    r = para.add_run(text)
    r.font.name = "Cambria Math"
    r.font.size = Pt(11.5)
    return para


def code(text):
    para = doc.add_paragraph()
    para.paragraph_format.left_indent = Cm(0.6)
    r = para.add_run(text)
    r.font.name = "Consolas"
    r.font.size = Pt(9.5)
    return para


def shade(cell, hex_color):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), hex_color)
    tc_pr.append(shd)


def table(headers, rows, widths=None, font_size=9.5):
    t = doc.add_table(rows=1, cols=len(headers))
    t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    for i, h in enumerate(headers):
        c = t.rows[0].cells[i]
        c.text = ""
        run = c.paragraphs[0].add_run(h)
        run.bold = True
        run.font.size = Pt(font_size)
        shade(c, "DCE3EE")
    for row in rows:
        cells = t.add_row().cells
        for i, val in enumerate(row):
            cells[i].text = ""
            run = cells[i].paragraphs[0].add_run(val)
            run.font.size = Pt(font_size)
    if widths:
        for row in t.rows:
            for i, w in enumerate(widths):
                row.cells[i].width = Cm(w)
    doc.add_paragraph()
    return t


def figure(name, caption, width_cm=16.0):
    path = IMG / name
    if not path.exists():
        raise FileNotFoundError(path)
    doc.add_picture(str(path), width=Cm(width_cm))
    doc.paragraphs[-1].alignment = WD_ALIGN_PARAGRAPH.CENTER
    cap = p(caption, italic=True, align=WD_ALIGN_PARAGRAPH.CENTER)
    cap.runs[0].font.size = Pt(9.5)


# =====================================================================
# Title page
# =====================================================================
for _ in range(5):
    doc.add_paragraph()
t = p("Bloch-gömb szimulátor", align=WD_ALIGN_PARAGRAPH.CENTER)
t.runs[0].font.size = Pt(28)
t.runs[0].bold = True
t.runs[0].font.color.rgb = RGBColor(0x1F, 0x2A, 0x44)
s = p("Házi feladat – 2. feladat: Kvantuminformatikai alapok", align=WD_ALIGN_PARAGRAPH.CENTER)
s.runs[0].font.size = Pt(14)
s2 = p("Kvantuminformatikai alkalmazások (BMEVIHIAD00), 2026 ősz", align=WD_ALIGN_PARAGRAPH.CENTER)
s2.runs[0].font.size = Pt(12)
doc.add_paragraph()
doc.add_paragraph()
p("Felhasználói és fejlesztői dokumentáció", align=WD_ALIGN_PARAGRAPH.CENTER).runs[0].font.size = Pt(12)
doc.add_paragraph()
for line in ("Készítette: ………………………………………", "Neptun-kód: ………………", "Beadás: 2026. november 15."):
    p(line, align=WD_ALIGN_PARAGRAPH.CENTER)
doc.add_page_break()

# =====================================================================
# Contents (static list, kept short)
# =====================================================================
doc.add_heading("Tartalom", level=1)
for item in (
    "1. Bevezetés és a feladat összefoglalása",
    "2. A program futtatása",
    "3. Felhasznált technológiák és könyvtárak",
    "4. Elméleti háttér és jelölések",
    "5. Kezelési útmutató",
    "6. Megfelelés az értékelési szempontoknak",
    "7. A program felépítése",
    "8. Tesztelés és ellenőrzés",
    "9. Korlátok és továbbfejlesztési lehetőségek",
    "10. Források",
):
    p(item)
doc.add_page_break()

# =====================================================================
# 1. Introduction
# =====================================================================
doc.add_heading("1. Bevezetés és a feladat összefoglalása", level=1)
p("A házi feladat célja egy Bloch-gömb szimulátor elkészítése, amely egyetlen kvantumbit (qubit) állapotát és "
  "az egybites kvantumkapuk hatását szemlélteti. A feladatkiírás szerint a programnak minél több kaput kell "
  "kínálnia (alapkapuk, forgatási és fáziskapuk), ezeknek helyesen kell működniük, egymás után fűzhetőnek kell "
  "lenniük, a programnak egyszerre több állapotot is meg kell tudnia jeleníteni, és tetszőleges kezdőállapotot "
  "kell tudni megadni.")
p("Az elkészült alkalmazás egy Windowsos asztali program, amely 3D-ben, egérrel forgatható Bloch-gömbön mutatja "
  "a qubit állapotvektorát. A kapuk hatását animáció mutatja: a vektor a kapu forgástengelye körül, a tényleges "
  "pályán mozog az új állapotba. A korábbi állapotok sorszámozva és a pályájukkal együtt a gömbön maradnak. Az "
  "aktuális állapot számszerűen is látható (α, β, θ, φ, Bloch-koordináták, mérési valószínűségek). Mérni is lehet "
  "Z- és X-bázisban, egyszeri méréssel vagy sokszor ismételt méréssel és hisztogrammal. Legfeljebb hat független "
  "qubit lehet egyszerre a gömbön, mindegyik saját színnel és saját műveleti lánccal.")
p("A program az előadásokon használt jelöléseket követi: a képzetes egységet j jelöli, a fázisforgató kapu "
  "P(α), és az állapot alakja |ψ⟩ = cos(θ/2)|0⟩ + e^(jφ)·sin(θ/2)|1⟩.")

# =====================================================================
# 2. Running
# =====================================================================
doc.add_heading("2. A program futtatása", level=1)
doc.add_heading("2.1 Rendszerkövetelmények", level=2)
bullet("64 bites Windows 10 vagy Windows 11.")
bullet("Nem kell hozzá telepíteni semmit: sem .NET futtatókörnyezetet, sem fejlesztőkörnyezetet. A .NET 8 "
       "futtatókörnyezet és minden felhasznált könyvtár bele van csomagolva a futtatható állományba "
       "(„self-contained, single-file” kiadás).")
bullet("Kb. 45 MB szabad hely (a .exe mérete 43 MB). Az első indításkor a program a beágyazott natív részeket egy ideiglenes mappába "
       "bontja ki, ezért az első indulás néhány másodpercig tarthat.")
doc.add_heading("2.2 Indítás", level=2)
numbered("Másold a BlochSphereSimulator.exe fájlt egy tetszőleges mappába (pl. az Asztalra).")
numbered("Indítsd el dupla kattintással.")
numbered("Ha a Windows SmartScreen figyelmeztet („A Windows megvédte a számítógépet”), mert a fájl nincs "
         "digitálisan aláírva, válaszd a „További információ”, majd a „Futtatás mindenképp” lehetőséget.")
p("A program ablaka átméretezhető és teljes képernyőre is nagyítható. Kisebb képernyőn a jobb oldali vezérlőpanel "
  "görgethető.")
doc.add_heading("2.3 Fordítás forráskódból (opcionális)", level=2)
p("A forráskód fordításához .NET 8 SDK (vagy újabb) szükséges. A projekt gyökérmappájában:")
code("dotnet build BlochSphereSimulator.sln\n"
     "dotnet test  BlochSphereSimulator.sln      (unit tesztek)\n"
     "publish.cmd                                (önálló .exe a dist\\ mappába)")
p("A publish.cmd a dotnet publish -c Release -r win-x64 parancsot futtatja; a kiadás beállításai a projektfájlban "
  "(BlochSphereSimulator.csproj) vannak. A futtatható állomány önálló (self-contained), egyetlen fájlból áll, "
  "tömörített, csak az angol nyelvű keretrendszer-erőforrásokat tartalmazza, és a .NET alapkönyvtáraiból a "
  "linker (trimming) eltávolítja a nem használt kódot. A WPF és a XAML-ből betöltött könyvtárak (HelixToolkit, "
  "MathNet.Numerics) teljes egészükben megmaradnak, mert ezeket reflexióval használja a keretrendszer. Így a "
  "méret 157 MB-ról 43 MB-ra csökkent, és a fájl elfér a Moodle 50 MB-os feltöltési korlátjában.")

# =====================================================================
# 3. Libraries
# =====================================================================
doc.add_heading("3. Felhasznált technológiák és könyvtárak", level=1)
table(
    ["Komponens", "Verzió", "Licenc", "Mire használja a program"],
    [
        ["C# / .NET", "8.0 (net8.0-windows)", "MIT", "programnyelv és futtatókörnyezet"],
        ["WPF (Windows Presentation Foundation)", ".NET 8 része", "MIT", "grafikus felület, adatkötés (MVVM), 3D megjelenítés alapja"],
        ["HelixToolkit.Wpf", "3.1.2", "MIT", "3D nézet: kamera, egérrel forgatás/nagyítás, nyilak, vonalak, gömb, 3D feliratok"],
        ["MathNet.Numerics", "5.0.0", "MIT", "komplex mátrixok és vektorok (kapuk, állapotvektor, mátrixszorzás)"],
        ["System.Numerics.Complex", ".NET 8 része", "MIT", "komplex számok"],
        ["MSTest (TestFramework, TestAdapter)", "3.6.2", "MIT", "unit tesztek"],
        ["Microsoft.NET.Test.Sdk", "17.11.1", "MIT", "tesztfuttatás"],
    ],
    widths=[4.6, 2.8, 1.4, 7.6],
)
p("A HelixToolkit.Wpf és a MathNet.Numerics NuGet-csomagként kerül a projektbe; a dotnet restore automatikusan "
  "letölti őket. A kész .exe már tartalmazza őket, a felhasználónak nem kell külön telepítenie.")

# =====================================================================
# 4. Theory
# =====================================================================
doc.add_heading("4. Elméleti háttér és jelölések", level=1)
doc.add_heading("4.1 Állapotleírás (1. posztulátum) és a Bloch-gömb", level=2)
p("Egy qubit állapota egységnyi hosszú komplex vektor a kétdimenziós Hilbert-térben:")
formula("|ψ⟩ = α|0⟩ + β|1⟩,    α, β ∈ ℂ,    |α|² + |β|² = 1")
p("A globális fázis fizikailag nem mérhető, ezért α valósnak és nemnegatívnak választható. Így minden állapot "
  "két szöggel írható le, ahogy az előadáson is:")
formula("|ψ⟩ = cos(θ/2)|0⟩ + e^(jφ)·sin(θ/2)|1⟩,    0 ≤ θ ≤ 180°,  0 ≤ φ < 360°")
p("Az Összefonódás előadás ugyanezt az alakot α (poláris) és β (azimut) szögekkel írja fel, egy e^(jγ) globális "
  "fázissal: |φ⟩ = e^(jγ)[cos(α/2)|0⟩ + e^(jβ)sin(α/2)|1⟩]. A program a Posztulátumok előadás θ, φ jelölését "
  "használja a kezdőállapothoz, az α betűt pedig a fázis- és forgatókapuk szögéhez.")
p("θ és φ a Bloch-gömb egy pontjának gömbi koordinátái. A program a következő képletekkel számolja a "
  "Bloch-vektort:")
formula("x = 2·Re(α*β),    y = 2·Im(α*β),    z = |α|² − |β|²")
p("A pólusok a bázisállapotok: |0⟩ az északi pólus (+z), |1⟩ a déli pólus (−z). Az egyenlítőn vannak a "
  "|±⟩ = (|0⟩ ± |1⟩)/√2 (±x) és a |±j⟩ = (|0⟩ ± j|1⟩)/√2 (±y) állapotok. A program a tengelyeken ezeket a "
  "neveket is kiírja. A kijelzett α és β értékek mindig a globális fázis elhagyásával (α valós, nemnegatív) "
  "látszanak. Ezért például a fázisforgatás után kapott e^(jα/2)-es szorzó a számokban nem jelenik meg, a gömbön "
  "pedig eleve nem látszik.")

doc.add_heading("4.2 Kvantumkapuk (2. posztulátum)", level=2)
p("Zárt rendszer időbeli fejlődését unitér transzformáció írja le (U†U = I). Egy qubit esetén minden unitér "
  "kapu – egy globális fázistól eltekintve – a Bloch-gömb egy forgatása valamely tengely körül. A program ezt "
  "használja az animációhoz: minden kapuhoz tartozik egy forgástengely és egy forgásszög. A program az alábbi "
  "kapukat ismeri:")
table(
    ["Kapu", "Mátrix", "Forgatás a Bloch-gömbön"],
    [
        ["Pauli-X (bit-flip)", "[[0, 1], [1, 0]]", "180° az x tengely körül"],
        ["Pauli-Y", "[[0, −j], [j, 0]]", "180° az y tengely körül"],
        ["Pauli-Z (phase-flip)", "[[1, 0], [0, −1]]", "180° a z tengely körül"],
        ["Hadamard (H)", "1/√2 · [[1, 1], [1, −1]]", "180° az (x+z)/√2 tengely körül"],
        ["Fázisforgató P(α)", "[[1, 0], [0, e^(jα)]]", "α a z tengely körül"],
        ["S = P(90°)", "[[1, 0], [0, j]]", "90° a z tengely körül"],
        ["S† = P(−90°)", "[[1, 0], [0, −j]]", "−90° a z tengely körül"],
        ["T = P(45°)", "[[1, 0], [0, e^(jπ/4)]]", "45° a z tengely körül"],
        ["T† = P(−45°)", "[[1, 0], [0, e^(−jπ/4)]]", "−45° a z tengely körül"],
        ["√X", "½ · [[1+j, 1−j], [1−j, 1+j]]", "90° az x tengely körül (√X·√X = X)"],
        ["Rx(α)", "[[cos(α/2), −j·sin(α/2)], [−j·sin(α/2), cos(α/2)]]", "α az x tengely körül"],
        ["Ry(α)", "[[cos(α/2), −sin(α/2)], [sin(α/2), cos(α/2)]]", "α az y tengely körül"],
        ["Rz(α)", "[[e^(−jα/2), 0], [0, e^(jα/2)]]", "α a z tengely körül (P(α)-tól csak globális fázisban tér el)"],
    ],
    widths=[3.6, 7.0, 5.8],
)
p("A mátrixok megegyeznek az előadásfóliákon szereplőkkel. Például a fóliák szerint Y(a, b) = (−jb, ja), "
  "Z(a, b) = (a, −b) és P(α)(a, b) = (a, e^(jα)b); ezeket unit tesztek ellenőrzik. Az S és T kapuk a P(α) "
  "fáziskapu speciális esetei, a program felülete is így nevezi meg őket.")

doc.add_heading("4.3 Mérés (3. posztulátum)", level=2)
p("A program projektív (Neumann-) mérést valósít meg. Z-bázisban a mérési operátorok a P₀ = |0⟩⟨0| és "
  "P₁ = |1⟩⟨1| projektorok, amelyekre teljesül a teljességi reláció (P₀ + P₁ = I). Az m eredmény valószínűsége "
  "P(m) = ⟨ψ|Pₘ|ψ⟩, azaz P(0) = |α|² és P(1) = |β|². A mérés utáni állapot:")
formula("|ψ′⟩ = Pₘ|ψ⟩ / √P(m)")
p("Vagyis az állapot a mért bázisállapotba „billen”. A projektor idempotens (P₀P₀ = P₀), ezért az azonnal "
  "megismételt mérés mindig ugyanazt az eredményt adja. Ezt a program és egy unit teszt is mutatja. (Megjegyzés: "
  "a fólián a nevezőben |a|² szerepel, de a helyes normálás √P(0) = |a|.)")
p("X-bázisban (Hadamard-bázis, ahogy a BB84 protokollban) a mérés a |+⟩, |−⟩ bázisállapotok szerint történik: "
  "P(±) = |⟨±|ψ⟩|² = |α ± β|²/2, és az állapot |+⟩-ba vagy |−⟩-ba billen.")
p("Az ismételt mérés funkció a valós kvantumszámítógépeken szokásos „shot”-okat utánozza (vö. az "
  "interferométeres fólia shots=10000 paraméterével). Ilyenkor a program ugyanazt az állapotot N-szer előállítja "
  "és megméri, majd hisztogramon mutatja a gyakoriságokat az elméleti valószínűséggel együtt. A megjelenített "
  "állapot ettől nem változik.")

# =====================================================================
# 5. User guide
# =====================================================================
doc.add_heading("5. Kezelési útmutató", level=1)
figure("01_start.png", "1. ábra – A program felülete indítás után: balra a 3D Bloch-gömb a kijelzőkkel, jobbra a vezérlőpanel")
p("A felület két részből áll. A bal oldalon a 3D nézet van két átlátszó panellel. Bal felül az aktuális állapot "
  "adatai láthatók, bal alul a műveleti lánc és a megjelenítési beállítások. A jobb oldalon az öt számozott "
  "vezérlőcsoport van, felülről lefelé a használat sorrendjében.")

doc.add_heading("5.1 A 3D nézet", level=2)
bullet("Forgatás: bal egérgomb lenyomva tartva és húzva. Mozgatás: jobb egérgomb. Nagyítás: egérgörgő. A „Nézet "
       "alaphelyzetbe” gomb visszaállítja a kezdeti kameraállást.")
bullet("A tengelyek: x (piros, |+⟩), y (zöld, |+j⟩), z (kék, |0⟩). Az ellentétes oldalon |−⟩, |−j⟩ és |1⟩ látható. "
       "Az egyenlítő sötétebb vonallal van kiemelve.")
bullet("Az aktuális állapot vastag, a qubit színével megegyező nyíl. A korábbi állapotok vékonyabb, halványabb "
       "nyilak; minél régebbi egy állapot, annál halványabb. A kiválasztott qubitnél a nyílvégeken lévő sorszám "
       "mutatja a lépések sorrendjét (0 = kezdőállapot). Ha több lépés ugyanabba a pontba vezet, közös "
       "címkét kapnak (pl. „0, 4”). A címkék mindig közvetlenül a nyílhegy mögött, a nyíl irányában jelennek meg.")
bullet("A pályák azt mutatják, merre mozdult a vektor egy kapu hatására. Kapunál ez egy körív a forgástengely "
       "körül, mérésnél szaggatott egyenes, mert a mérés nem unitér, hanem „ugrás”.")

doc.add_heading("5.2 Az aktuális állapot kijelzője", level=2)
p("A bal felső panel a kiválasztott qubit aktuális állapotát mutatja: a |ψ⟩ képletet a konkrét szögekkel, "
  "α-t és β-t j-s alakban, a θ és φ szögeket, a Bloch-koordinátákat (x, y, z), a Z-bázisú mérési "
  "valószínűségeket számmal és sávdiagrammal, valamint az X-bázisú mérési valószínűségeket. Minden adat azonnal "
  "frissül, amikor egy kapu vagy mérés hat a qubitre.")

doc.add_heading("5.3 Kezdőállapot megadása", level=2)
bullet("θ (0–180°) és φ (0–360°): csúszkával vagy a mellettük lévő számmezőbe gépelve (Enter vagy Tab). A számmező "
       "tizedes értéket is elfogad, a tartományon kívüli értéket a program a határra korlátozza.")
bullet("Amíg a qubitre még nem hatott kapu, a csúszkák élőben mozgatják a kezdőállapotot. Ha már van műveleti "
       "lánc, a csúszka nem törli azt, hanem figyelmeztet. Az új kezdőállapot ilyenkor az „Új kezdőállapot (θ, φ)” "
       "gombbal indítható.")
bullet("Nevezetes állapotok gombjai: |0⟩, |1⟩, |+⟩, |−⟩, |+j⟩, |−j⟩. Egy kattintással beállítják a szögeket és "
       "újraindítják a qubitet a választott állapotból.")
figure("02_typed_state.png", "2. ábra – Kezdőállapot számmal megadva (θ = 60°, φ = 120°), majd egy kapulánc a sorszámozott lépésekkel és pályákkal", 15.0)

doc.add_heading("5.4 Kapuk alkalmazása és láncolása", level=2)
p("A 2. csoport az alapkapukat (X, Y, Z, H), a 3. csoport a fáziskapukat és egyéb nevezetes kapukat (S, S†, T, "
  "T†, √X), a 4. csoport a paraméteres kapukat (P(α), Rx(α), Ry(α), Rz(α)) tartalmazza. A paraméteres kapuk "
  "szöge csúszkával vagy számmezővel adható meg; negatív érték (−360°…360°) is beírható. Minden gomb fölé "
  "állva a kapu mátrixa és geometriai jelentése is megjelenik.")
p("Kattintáskor a kapu a kiválasztott qubit aktuális állapotára hat. A program ehhez a kapu mátrixát szorozza "
  "az állapotvektorral, így a kapuk tetszőleges hosszú láncba fűzhetők. A lánc a bal alsó panelen olvasható, "
  "például: Init(|0⟩) ➝ H ➝ T ➝ S ➝ √X.")
bullet("Animáció: a vektor a kapu forgástengelye körül, a valódi pályán fordul el; a tengelyt szaggatott vonal "
       "jelzi. Az animáció kikapcsolható, a sebessége állítható. Ha a Windowsban ki vannak kapcsolva az "
       "animációk, a program alapból animáció nélkül indul.")
bullet("Visszavonás (Ctrl+Z): az utolsó kapu vagy mérés visszavonása. „Lánc törlése”: vissza a qubit "
       "kezdőállapotához.")
bullet("A „Korábbi állapotok” és a „Pályák” jelölőnégyzettel a gömb tartalma egyszerűsíthető.")
figure("03_animation_H.png", "3. ábra – A Hadamard-kapu animációja |0⟩-ból: a vektor az (x+z)/√2 tengely körül fordul |+⟩ felé", 15.0)
figure("04_chain_history.png", "4. ábra – Kapulánc (H, T, S, √X) a sorszámozott korábbi állapotokkal és a forgatások pályáival", 15.0)

doc.add_heading("5.5 Mérés", level=2)
bullet("„Mérés Z-bázisban” / „Mérés X-bázisban”: egyszeri projektív mérés. A program véletlenszerűen, a "
       "valószínűségeknek megfelelően választ eredményt, a lánchoz hozzáadja (pl. M_Z=0), kiírja az eredményt "
       "és annak valószínűségét, a vektor pedig a mért bázisállapotba ugrik. A mérés is visszavonható.")
bullet("Ismételt mérés: válaszd ki a mérések számát (100, 1000 vagy 10000) és a bázist, majd „Futtatás”. A "
       "hisztogram a mért gyakoriságokat mutatja, a fekete függőleges vonal az elméleti értéket. Ha az állapot "
       "később megváltozik, a régi hisztogram törlődik, hogy ne lehessen összekeverni.")
figure("05_shots.png", "5. ábra – 1000 ismételt mérés Z-bázisban: a mért gyakoriság az elméleti 85,4% körül szóródik (fekete vonal)", 15.0)
figure("05_measurement.png", "6. ábra – Egyszeri mérés után az állapot |0⟩-ba billent (szaggatott pálya), az eredmény a láncban és a panelen is látszik", 15.0)

doc.add_heading("5.6 Több qubit egyszerre", level=2)
p("A „Kvantumbitek” csoportban az „+ Új qubit” gomb egy új, független qubitet hoz létre (legfeljebb hatot) a "
  "jelenlegi θ, φ kezdőállapottal. Minden qubitnek saját színe, neve (q0, q1, …), műveleti lánca és előzménye "
  "van. A kapuk, a mérés és a kijelzők mindig a listában kiválasztott qubitre vonatkoznak; a többi qubit "
  "vékonyabb nyíllal, névvel feliratozva látszik. Így közvetlenül összehasonlítható, hogy ugyanaz a kapu hogyan "
  "hat különböző állapotokra.")
figure("06_multiple_qubits.png", "7. ábra – Három független qubit (q0, q1, q2) saját színnel, előzménnyel és pályával", 15.0)

doc.add_heading("5.7 Példa: a kvantuminterferométer az előadásból", level=2)
p("Az interferométeres előadás áramköre H → P(Δα) → H, majd mérés sok ismétléssel (shots=10000). A szimulátorban "
  "így reprodukálható: „|0⟩” gomb, H, a szöget Δα-ra állítva P(α), ismét H, majd ismételt mérés 10000-rel "
  "Z-bázisban. Az elmélet szerint H·P(Δα)·H|0⟩ = ((1+e^(jΔα))/2)|0⟩ + ((1−e^(jΔα))/2)|1⟩, tehát P(0) = cos²(Δα/2). "
  "Δα = 0° esetén minden mérés 0, Δα = 180° esetén minden mérés 1, Δα = 60° esetén P(0) = 0,75. A gömbön jól "
  "látszik a háttérben zajló folyamat: az első H az egyenlítőre viszi a vektort, P(Δα) az egyenlítő mentén "
  "elforgatja, a második H pedig visszaforgatja, így a fáziskülönbségből valószínűségi különbség lesz "
  "(interferencia).")

# =====================================================================
# 6. Requirements
# =====================================================================
doc.add_heading("6. Megfelelés az értékelési szempontoknak", level=1)
table(
    ["Szempont (2. értékelési szempont)", "Megvalósítás a programban"],
    [
        ["Mennyi és milyen kapuk érhetők el?",
         "13 kapu: X, Y, Z, H; P(α), S, S†, T, T†; √X; Rx(α), Ry(α), Rz(α) tetszőleges (negatív is) szöggel."],
        ["Ezek helyesen működnek?",
         "A mátrixok egyeznek a fóliákkal. Unit tesztek ellenőrzik az unitaritást, a nevezetes azonosságokat "
         "(H² = I, S = T², Z = S², √X² = X), és azt, hogy az animáció végpontja mindig U|ψ⟩ Bloch-pontja "
         "(13 kapu × 50 véletlen állapot)."],
        ["Van-e lehetőség ezeket egymásba fűzni?",
         "Igen, tetszőleges hosszú lánc, a lánc szövegesen és a gömbön (sorszámozott állapotok, pályák) is "
         "látszik, visszavonással (Ctrl+Z)."],
        ["Tud több állapotot egyszerre mutatni?",
         "Igen, kétféleképpen: egy qubit teljes előzménye (minden korábbi állapot) és legfeljebb 6 független "
         "qubit egyszerre, eltérő színnel."],
        ["Tetszőleges kezdőállapot megadható?",
         "Igen: θ, φ csúszkával vagy pontos számértékkel (tizedes értékkel is), valamint 6 nevezetes állapot "
         "gombbal."],
    ],
    widths=[5.5, 10.9],
)
p("Az 1. szempont (dokumentáció) a jelen, legalább háromoldalas dokumentum, amely tartalmazza a használathoz "
  "szükséges információkat és a felhasznált könyvtárakat (3. fejezet). A 3. szemponthoz (felhasználói élmény) "
  "kapcsolódó funkciók: animált forgatás a forgástengellyel, élő számszerű kijelző, sorszámozott előzmények, "
  "mérési hisztogram, magyarázó buborékok a gombokon és átméretezhető ablak. A felület bemutatóra is alkalmas.")
p("A beadási követelmények közül a forráskód a projekt mappájában, az önállóan futtatható program a "
  "dist\\BlochSphereSimulator.exe fájlban található, ez a dokumentáció pedig DOCX és PDF formátumban is elérhető.")

# =====================================================================
# 7. Architecture
# =====================================================================
doc.add_heading("7. A program felépítése", level=1)
p("A program a WPF-ben szokásos MVVM (Model–View–ViewModel) mintát követi: a kvantummatematika független a "
  "felülettől, így felület nélkül, unit tesztekkel ellenőrizhető.")
table(
    ["Fájl", "Feladat"],
    [
        ["Models/QuantumMath.cs", "Kapumátrixok, kapukatalógus (mátrix + forgástengely + szög), állapotvektor θ, φ-ből, "
                                  "Bloch-koordináták és -szögek, globális fázis normálása, mérési valószínűségek Z/X "
                                  "bázisban, projektív mérés, ismételt mérés, Rodrigues-forgatás és pályaszámítás, j-s "
                                  "komplex szám formázás."],
        ["Models/QubitTrack.cs", "Egy qubit: név, szín, lépések listája (QubitStep: típus, címke, állapot, Bloch-pont, "
                                 "pálya, tengely), visszavonás, újraindítás, láncszöveg."],
        ["ViewModels/MainViewModel.cs", "A felület állapota és parancsai: bemenetek, kapuk, mérés, hisztogram, qubitek "
                                        "kezelése, élő kijelző szövegei; események a 3D nézet "
                                        "újrarajzolásához és az animációhoz."],
        ["MainWindow.xaml", "A felület leírása (elrendezés, stílusok, adatkötések)."],
        ["MainWindow.xaml.cs", "A 3D jelenet: drótvázas gömb, tengelyek, feliratok, qubitnyilak, előzmények, pályák, "
                               "sorszámok; az animáció képkockánként (CompositionTarget.Rendering), ease-in-out "
                               "időzítéssel."],
        ["Commands/RelayCommand.cs", "Általános ICommand megvalósítás a gombokhoz."],
        ["BlochSphereSimulator.Tests/", "35 unit teszt (QuantumMathTests, MainViewModelTests)."],
        ["tools/", "A dokumentáció és a képernyőképek reprodukálható előállítása (UI Automation szkript, DOCX generátor)."],
    ],
    widths=[5.0, 11.4],
)
doc.add_heading("7.1 A kapuk végrehajtása és az animáció", level=2)
p("Kapu alkalmazásakor a ViewModel előbb kiszámolja az új állapotot mátrixszorzással (|ψ′⟩ = U|ψ⟩). Ezután "
  "elkészíti az animáció pályáját: a régi Bloch-vektort a kapu tengelye körül 48 lépésben forgatja el a "
  "kapu szögével, Rodrigues forgatási képletével:")
formula("v′ = v·cos φ + (k × v)·sin φ + k·(k·v)·(1 − cos φ)")
p("Az új lépés bekerül a qubit előzményei közé, a nézet pedig végigvezeti rajta a nyilat. A paraméteres kapuk "
  "a megadott szöget használják, így például az Rz(270°) valóban 270°-os forgatásként jelenik meg, nem a rövidebb, "
  "−90°-os úton. Hogy a pálya és a mátrixszorzás eredménye biztosan egyezzen, azt a tesztek minden kapura "
  "ellenőrzik.")

# =====================================================================
# 8. Testing
# =====================================================================
doc.add_heading("8. Tesztelés és ellenőrzés", level=1)
p("A megoldás 35 automatikus unit tesztet tartalmaz (dotnet test), mindegyik sikeresen lefut. Főbb csoportok:")
bullet("Kapuk: unitaritás mind a 13 kapura; H² = I, S = P(90°), T = P(45°), T² = S, S² = Z, SS† = TT† = I, "
       "√X² = X; a fóliákon szereplő Y, Z, P(α) hatás általános állapotra.", "Mátrixok – ")
bullet("H|0⟩ = |+⟩ (+x), X|0⟩ = |1⟩ (−z), P(90°)|+⟩ = |+j⟩ (+y); a 6 nevezetes állapot Bloch-pontja; "
       "θ, φ ↔ állapot oda-vissza konverzió; globális fázis normálása.", "Állapotok – ")
bullet("Az animációs pálya végpontja egyezik U|ψ⟩ Bloch-pontjával (13 kapu × 50 véletlen állapot és szög), és a pálya "
       "minden pontja a gömb felszínén van.", "Geometria – ")
bullet("Az előadás „tetszőleges állapot előállítása” levezetése (H → P(α) → H → P(90°+β)) 40 véletlen "
       "szögpárra a várt állapotot adja, az e^(jα/2) globális fázissal együtt; az interferométer-áramkör (H → P(Δα) → H) P(0) = cos²(Δα/2) statisztikát ad.", "Előadás-példák – ")
bullet("Z- és X-bázisú valószínűségek, összeomlás bázisállapotba, ismételt mérés ugyanazt adja, 100 000 mérés "
       "gyakorisága 1%-on belül egyezik az elméleti értékkel.", "Mérés – ")
bullet("Kapulánc, visszavonás, lánc törlése, a csúszkák nem törlik a meglévő láncot, bemenetek korlátozása, "
       "nevezetes állapotok, mérés, hisztogram, több független qubit, legfeljebb 6 qubit.",
       "Felületlogika (ViewModel) – ")
p("A grafikus felületet kézzel is kipróbáltam, és a dokumentáció képernyőképeit egy Windows UI Automation "
  "szkript készítette, amely valódi gombnyomásokkal hajtja végig a programot (tools/shots_scenario.ps1). Az "
  "önállóan futtatható .exe-t is kipróbáltuk: elindul, és kapuk, illetve mérés után a helyes eredményt mutatja.")

# =====================================================================
# 9. Limitations
# =====================================================================
doc.add_heading("9. Korlátok és továbbfejlesztési lehetőségek", level=1)
bullet("Összefonódás: a 4. posztulátum szerinti többqubites (pl. CNOT-tal létrehozott Bell-) állapot nem "
       "bontható fel két egyqubites állapot szorzatára, ezért nem ábrázolható pontokként a Bloch-gömbök felszínén. "
       "A program ezért kizárólag független qubitekkel dolgozik, és ezt a felületen is jelzi. Továbbfejlesztésként "
       "a redukált sűrűségmátrix Bloch-vektora (a gömb belsejében lévő pont) megjeleníthető lenne.")
bullet("Kevert állapotok és zaj (dekoherencia) szimulációja, például depolarizáló csatorna, amely a vektort a gömb "
       "középpontja felé húzza.")
bullet("Tetszőleges irányú mérési bázis megadása (pl. a CHSH-teszt 22,5°-os bázisaihoz).")
bullet("A műveleti lánc mentése és betöltése, valamint áramköri nézet a lánchoz.")

# =====================================================================
# 10. References
# =====================================================================
doc.add_heading("10. Források", level=1)
for ref in (
    "A tárgy előadásfóliái (Kvantuminformatikai alkalmazások, BME HIT, 2026 ősz): Kvantummechanikai posztulátumok "
    "és alkalmazásuk; Összefonódás; Mérések – átjáró a kvantum és a klasszikus világ között; A kvantuminterferométer "
    "működése és a No cloning tétel; Kvantuminformatikai rendszerek építőelemei; Tetszőleges kvantumbit előállítása, "
    "szupersűrűségű tömörítés, teleportálás; Kvantum alapú kulcsszétosztás.",
    "Oláh K.: Preparing arbitrary quantum state (segédanyag, 2025).",
    "S. Imre, F. Balázs: Quantum Computing and Communications – An Engineering Approach, John Wiley & Sons, 2005.",
    "Az előadáson ajánlott Bloch-gömb szimulátorok: https://bloch.kherb.io/ és https://www.justqlabs.com/simulator",
    "HelixToolkit dokumentáció: https://github.com/helix-toolkit/helix-toolkit",
    "Math.NET Numerics dokumentáció: https://numerics.mathdotnet.com/",
):
    bullet(ref)

p("A program fejlesztése során mesterséges intelligencián alapuló kódasszisztenst is használtam, a kari "
  "irányelveknek megfelelően. A megoldás helyességét a fenti unit tesztek és az előadásanyaggal való "
  "összevetés ellenőrzi.", italic=True)

# ---------- page numbers in footer ----------
for section in doc.sections:
    footer_p = section.footer.paragraphs[0]
    footer_p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = footer_p.add_run()
    for tag, text in (("begin", None), (None, "PAGE"), ("end", None)):
        if tag:
            el = OxmlElement("w:fldChar")
            el.set(qn("w:fldCharType"), tag)
        else:
            el = OxmlElement("w:instrText")
            el.set(qn("xml:space"), "preserve")
            el.text = text
        run._r.append(el)

OUT.parent.mkdir(parents=True, exist_ok=True)
doc.save(OUT)
print(f"Saved {OUT}")
