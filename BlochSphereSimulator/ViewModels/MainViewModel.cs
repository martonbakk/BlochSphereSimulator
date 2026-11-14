using BlochSphereSimulator.Commands;
using BlochSphereSimulator.Models;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Complex = System.Numerics.Complex;

namespace BlochSphereSimulator.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public const int MaxQubits = 6;
        private const double HistogramBarMaxWidth = 222;

        private static readonly Color[] QubitColors =
        {
            Color.FromRgb(220, 20, 60),   // crimson
            Color.FromRgb(30, 110, 230),  // blue
            Color.FromRgb(20, 160, 90),   // green
            Color.FromRgb(230, 130, 0),   // orange
            Color.FromRgb(140, 60, 200),  // purple
            Color.FromRgb(0, 160, 170),   // teal
        };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly Random _rng;
        private int _qubitCounter;

        public MainViewModel() : this(new Random()) { }

        public MainViewModel(Random rng)
        {
            _rng = rng;

            ApplyGateCommand = new RelayCommand(p => ApplyGate(p?.ToString()), _ => !IsBusy);
            SetInitStateCommand = new RelayCommand(_ => RestartSelectedFromInputs(), _ => !IsBusy);
            PresetCommand = new RelayCommand(p => ApplyPreset(p?.ToString()), _ => !IsBusy);
            UndoCommand = new RelayCommand(_ => Undo(), _ => !IsBusy && SelectedQubit?.CanUndo == true);
            ClearCommand = new RelayCommand(_ => ClearChain(), _ => !IsBusy);
            MeasureCommand = new RelayCommand(p => Measure(p?.ToString() == "X" ? MeasurementBasis.X : MeasurementBasis.Z), _ => !IsBusy);
            RunShotsCommand = new RelayCommand(_ => RunShots(), _ => !IsBusy);
            AddQubitCommand = new RelayCommand(_ => AddQubit(), _ => !IsBusy && Qubits.Count < MaxQubits);
            RemoveQubitCommand = new RelayCommand(_ => RemoveSelectedQubit(), _ => !IsBusy && Qubits.Count > 1);
            PreparationDemoCommand = new RelayCommand(async _ => await RunPreparationDemoAsync(), _ => !IsBusy);

            AddQubit();
        }

        // ================= Qubits =================

        public ObservableCollection<QubitTrack> Qubits { get; } = new();

        private QubitTrack? _selectedQubit;
        public QubitTrack? SelectedQubit
        {
            get => _selectedQubit;
            set
            {
                if (_selectedQubit == value || value == null) return;
                if (_selectedQubit != null) _selectedQubit.PropertyChanged -= OnSelectedQubitChanged;
                _selectedQubit = value;
                _selectedQubit.PropertyChanged += OnSelectedQubitChanged;
                OnPropertyChanged();
                RefreshReadout();
                SceneChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnSelectedQubitChanged(object? sender, PropertyChangedEventArgs e) => RefreshReadout();

        /// <summary>Raised when the whole 3D scene must be redrawn without animation.</summary>
        public event EventHandler? SceneChanged;

        /// <summary>Raised when a step was added that the view should animate.</summary>
        public event EventHandler<StepAddedEventArgs>? StepAdded;

        // ================= Inputs =================

        private double _inputTheta;
        /// <summary>Polar angle of the starting state in degrees (0..180).</summary>
        public double InputTheta
        {
            get => _inputTheta;
            set
            {
                value = Math.Clamp(Math.Round(value, 2), 0, 180);
                if (Math.Abs(_inputTheta - value) < 1e-9) return;
                _inputTheta = value;
                OnPropertyChanged();
                OnInputAnglesChanged();
            }
        }

        private double _inputPhi;
        /// <summary>Azimuthal angle of the starting state in degrees (0..360).</summary>
        public double InputPhi
        {
            get => _inputPhi;
            set
            {
                value = Math.Clamp(Math.Round(value, 2), 0, 360);
                if (Math.Abs(_inputPhi - value) < 1e-9) return;
                _inputPhi = value;
                OnPropertyChanged();
                OnInputAnglesChanged();
            }
        }

        private double _inputRotationAngle = 45;
        /// <summary>Angle (alpha) used by P(alpha), Rx, Ry and Rz, in degrees.</summary>
        public double InputRotationAngle
        {
            get => _inputRotationAngle;
            set
            {
                value = Math.Clamp(Math.Round(value, 2), -360, 360);
                if (Math.Abs(_inputRotationAngle - value) < 1e-9) return;
                _inputRotationAngle = value;
                OnPropertyChanged();
            }
        }

        private string _inputHint = "";
        /// <summary>Shown when the sliders changed but the chain was kept (gates were already applied).</summary>
        public string InputHint
        {
            get => _inputHint;
            private set { _inputHint = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// While only the initial state exists, the sliders move it live. Once gates were applied the
        /// chain is kept and the user has to confirm the new starting state explicitly.
        /// </summary>
        private void OnInputAnglesChanged()
        {
            if (SelectedQubit == null) return;
            if (!SelectedQubit.CanUndo)
            {
                RestartSelectedFromInputs();
            }
            else
            {
                InputHint = "A lánc megmaradt. Az új kezdőállapothoz kattints az „Új kezdőállapot” gombra.";
            }
        }

        // ================= Settings =================

        // Off by default when Windows animations are disabled (reduced-motion setting).
        private bool _animationEnabled = System.Windows.SystemParameters.ClientAreaAnimation;
        public bool AnimationEnabled
        {
            get => _animationEnabled;
            set { _animationEnabled = value; OnPropertyChanged(); }
        }

        private double _animationSeconds = 0.8;
        public double AnimationSeconds
        {
            get => _animationSeconds;
            set { _animationSeconds = Math.Clamp(value, 0.2, 3); OnPropertyChanged(); }
        }

        private bool _showHistory = true;
        public bool ShowHistory
        {
            get => _showHistory;
            set { _showHistory = value; OnPropertyChanged(); SceneChanged?.Invoke(this, EventArgs.Empty); }
        }

        private bool _showPaths = true;
        public bool ShowPaths
        {
            get => _showPaths;
            set { _showPaths = value; OnPropertyChanged(); SceneChanged?.Invoke(this, EventArgs.Empty); }
        }

        private bool _isBusy;
        /// <summary>True while the state-preparation demo runs; disables the other commands.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        // ================= Commands =================

        public ICommand ApplyGateCommand { get; }
        public ICommand SetInitStateCommand { get; }
        public ICommand PresetCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand MeasureCommand { get; }
        public ICommand RunShotsCommand { get; }
        public ICommand AddQubitCommand { get; }
        public ICommand RemoveQubitCommand { get; }
        public ICommand PreparationDemoCommand { get; }

        public IReadOnlyList<string> PresetNames { get; } = QuantumMath.Presets.Keys.ToList();

        // ================= Actions =================

        private string InitLabel() =>
            $"Init(θ={InputTheta.ToString("0.#", Inv)}°, φ={InputPhi.ToString("0.#", Inv)}°)";

        private void RestartSelectedFromInputs()
        {
            if (SelectedQubit == null) return;
            SelectedQubit.Restart(QuantumMath.GetStateVector(InputTheta, InputPhi), InitLabel());
            InputHint = "";
            LastMeasurementText = "";
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyPreset(string? name)
        {
            if (name == null || SelectedQubit == null || !QuantumMath.Presets.TryGetValue(name, out var angles)) return;
            _inputTheta = angles.Theta;
            _inputPhi = angles.Phi;
            OnPropertyChanged(nameof(InputTheta));
            OnPropertyChanged(nameof(InputPhi));
            SelectedQubit.Restart(QuantumMath.GetStateVector(angles.Theta, angles.Phi), $"Init({name})");
            InputHint = "";
            LastMeasurementText = "";
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyGate(string? gateName)
        {
            if (gateName == null || SelectedQubit == null) return;
            var gate = QuantumMath.GetGate(gateName, InputRotationAngle);
            ApplyGateDefinition(gate);
        }

        private void ApplyGateDefinition(GateDefinition gate)
        {
            var track = SelectedQubit!;
            var before = track.Current;
            var newState = gate.Matrix * before.State;
            var path = QuantumMath.RotationPath(before.Bloch, gate.Axis, gate.AngleDeg);
            var step = new QubitStep(StepKind.Gate, gate.Label, newState, path, gate.Axis);
            track.Push(step);
            StepAdded?.Invoke(this, new StepAddedEventArgs(track, step));
        }

        private void Undo()
        {
            if (SelectedQubit?.Undo() == true)
            {
                LastMeasurementText = "";
                SceneChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Removes all gates of the selected qubit and goes back to its first (initial) state.</summary>
        private void ClearChain()
        {
            if (SelectedQubit == null) return;
            var init = SelectedQubit.Steps[0];
            SelectedQubit.Restart(init.State, init.Label);
            LastMeasurementText = "";
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Measure(MeasurementBasis basis)
        {
            if (SelectedQubit == null) return;
            var track = SelectedQubit;
            var before = track.Current;
            double p0 = basis == MeasurementBasis.Z ? QuantumMath.ProbabilitiesZ(before.State).P0 : QuantumMath.ProbabilitiesX(before.State).PPlus;
            var (outcome, post) = QuantumMath.Measure(before.State, basis, _rng);
            string outcomeText = basis == MeasurementBasis.Z ? outcome.ToString(Inv) : (outcome == 0 ? "+" : "−");
            string label = $"M_{basis}={outcomeText}";
            var path = QuantumMath.StraightPath(before.Bloch, QuantumMath.GetBlochCoordinates(post));
            var step = new QubitStep(StepKind.Measurement, label, post, path);
            track.Push(step);

            double p = outcome == 0 ? p0 : 1 - p0;
            string ket = basis == MeasurementBasis.Z ? (outcome == 0 ? "|0⟩" : "|1⟩") : (outcome == 0 ? "|+⟩" : "|−⟩");
            LastMeasurementText = $"Eredmény: {outcomeText}  (valószínűsége {p.ToString("P1", Inv)}). Az állapot {ket}-ba „bebillent”.";
            StepAdded?.Invoke(this, new StepAddedEventArgs(track, step));
        }

        private void AddQubit()
        {
            if (Qubits.Count >= MaxQubits) return;
            var color = QubitColors[_qubitCounter % QubitColors.Length];
            _qubitCounter++;
            var track = new QubitTrack($"q{_qubitCounter - 1}", color, QuantumMath.GetStateVector(InputTheta, InputPhi), InitLabel());
            Qubits.Add(track);
            SelectedQubit = track;
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RemoveSelectedQubit()
        {
            if (Qubits.Count <= 1 || SelectedQubit == null) return;
            int index = Qubits.IndexOf(SelectedQubit);
            var toRemove = SelectedQubit;
            SelectedQubit = Qubits[index == 0 ? 1 : index - 1];
            toRemove.PropertyChanged -= OnSelectedQubitChanged;
            Qubits.Remove(toRemove);
            SceneChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Lecture "preparing an arbitrary quantum state": |0⟩ → H → P(α) → H → P(90°+β)
        /// gives e^(jα/2)[cos(α/2)|0⟩ + e^(jβ) sin(α/2)|1⟩], i.e. the Bloch point (θ=α, φ=β).
        /// The target is taken from the θ/φ inputs.
        /// </summary>
        public async Task RunPreparationDemoAsync(int? stepDelayMs = null)
        {
            if (SelectedQubit == null || IsBusy) return;
            IsBusy = true;
            try
            {
                double alpha = InputTheta;
                double beta = InputPhi;
                int delay = stepDelayMs ?? (AnimationEnabled ? (int)(AnimationSeconds * 1000) + 250 : 300);

                SelectedQubit.Restart(QuantumMath.GetStateVector(0, 0), $"Init(|0⟩) [cél: θ={alpha.ToString("0.#", Inv)}°, φ={beta.ToString("0.#", Inv)}°]");
                InputHint = "";
                LastMeasurementText = "";
                SceneChanged?.Invoke(this, EventArgs.Empty);
                if (delay > 0) await Task.Delay(delay);

                var sequence = new[]
                {
                    QuantumMath.GetGate("H", 0),
                    QuantumMath.GetGate("P", alpha),
                    QuantumMath.GetGate("H", 0),
                    QuantumMath.GetGate("P", 90 + beta),
                };
                foreach (var gate in sequence)
                {
                    ApplyGateDefinition(gate);
                    if (delay > 0) await Task.Delay(delay);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ================= Shots / histogram =================

        public IReadOnlyList<int> ShotOptions { get; } = new[] { 100, 1000, 10000 };

        private int _shotCount = 1000;
        public int ShotCount
        {
            get => _shotCount;
            set { _shotCount = value; OnPropertyChanged(); }
        }

        private bool _shotBasisIsX;
        public bool ShotBasisIsX
        {
            get => _shotBasisIsX;
            set { _shotBasisIsX = value; OnPropertyChanged(); }
        }

        private string _histogramLabel0 = "|0⟩", _histogramLabel1 = "|1⟩";
        private string _histogramSummary = "Válassz mérésszámot és bázist, majd „Futtatás”.";
        private double _histogramBar0, _histogramBar1, _expectedMarker0, _expectedMarker1;
        private string _histogramCount0 = "", _histogramCount1 = "";

        public string HistogramLabel0 { get => _histogramLabel0; private set { _histogramLabel0 = value; OnPropertyChanged(); } }
        public string HistogramLabel1 { get => _histogramLabel1; private set { _histogramLabel1 = value; OnPropertyChanged(); } }
        public string HistogramCount0 { get => _histogramCount0; private set { _histogramCount0 = value; OnPropertyChanged(); } }
        public string HistogramCount1 { get => _histogramCount1; private set { _histogramCount1 = value; OnPropertyChanged(); } }
        public double HistogramBar0 { get => _histogramBar0; private set { _histogramBar0 = value; OnPropertyChanged(); } }
        public double HistogramBar1 { get => _histogramBar1; private set { _histogramBar1 = value; OnPropertyChanged(); } }
        public double ExpectedMarker0 { get => _expectedMarker0; private set { _expectedMarker0 = value; OnPropertyChanged(); } }
        public double ExpectedMarker1 { get => _expectedMarker1; private set { _expectedMarker1 = value; OnPropertyChanged(); } }
        public string HistogramSummary { get => _histogramSummary; private set { _histogramSummary = value; OnPropertyChanged(); } }

        public (int Count0, int Count1) LastShots { get; private set; }

        /// <summary>
        /// Repeats "prepare the current state, then measure" N times, like shots on a real device
        /// (see the interferometer slide: shots=10000). The displayed state is not changed.
        /// </summary>
        private void RunShots()
        {
            if (SelectedQubit == null) return;
            var basis = ShotBasisIsX ? MeasurementBasis.X : MeasurementBasis.Z;
            var state = SelectedQubit.Current.State;
            var counts = QuantumMath.SampleShots(state, basis, ShotCount, _rng);
            LastShots = counts;
            double p0 = basis == MeasurementBasis.Z ? QuantumMath.ProbabilitiesZ(state).P0 : QuantumMath.ProbabilitiesX(state).PPlus;

            HistogramLabel0 = basis == MeasurementBasis.Z ? "|0⟩" : "|+⟩";
            HistogramLabel1 = basis == MeasurementBasis.Z ? "|1⟩" : "|−⟩";
            HistogramCount0 = $"{counts.Count0} ({((double)counts.Count0 / ShotCount).ToString("P1", Inv)})";
            HistogramCount1 = $"{counts.Count1} ({((double)counts.Count1 / ShotCount).ToString("P1", Inv)})";
            HistogramBar0 = HistogramBarMaxWidth * counts.Count0 / ShotCount;
            HistogramBar1 = HistogramBarMaxWidth * counts.Count1 / ShotCount;
            ExpectedMarker0 = HistogramBarMaxWidth * p0;
            ExpectedMarker1 = HistogramBarMaxWidth * (1 - p0);
            HistogramSummary = $"{ShotCount} mérés {basis}-bázisban ({SelectedQubit.Name}). Elméleti érték: {p0.ToString("P1", Inv)} / {(1 - p0).ToString("P1", Inv)} (fekete vonal).";
        }

        // ================= Live readout =================

        private string _lastMeasurementText = "";
        public string LastMeasurementText
        {
            get => _lastMeasurementText;
            private set { _lastMeasurementText = value; OnPropertyChanged(); }
        }

        public string AlphaText { get; private set; } = "";
        public string BetaText { get; private set; } = "";
        public string FormulaText { get; private set; } = "";
        public string AnglesText { get; private set; } = "";
        public string CoordinatesText { get; private set; } = "";
        public string ProbZText { get; private set; } = "";
        public string ProbXText { get; private set; } = "";
        public double ProbBar0 { get; private set; }
        public double ProbBar1 { get; private set; }
        public string ChainText => SelectedQubit?.ChainText ?? "";

        /// <summary>The histogram belongs to one specific state; it is cleared when that state changes.</summary>
        private void ClearHistogram()
        {
            if (HistogramBar0 == 0 && HistogramBar1 == 0) return;
            HistogramCount0 = HistogramCount1 = "";
            HistogramBar0 = HistogramBar1 = ExpectedMarker0 = ExpectedMarker1 = 0;
            HistogramSummary = "Az állapot megváltozott – futtasd újra a mérést.";
            LastShots = (0, 0);
        }

        private void RefreshReadout()
        {
            if (SelectedQubit == null) return;
            ClearHistogram();
            var raw = SelectedQubit.Current.State;
            var state = QuantumMath.NormalizeGlobalPhase(raw);
            var (theta, phi) = QuantumMath.GetBlochAngles(raw);
            var p = QuantumMath.GetBlochCoordinates(raw);
            var (p0, p1) = QuantumMath.ProbabilitiesZ(raw);
            var (pp, pm) = QuantumMath.ProbabilitiesX(raw);

            AlphaText = $"α = {QuantumMath.FormatComplex(state[0])}";
            BetaText = $"β = {QuantumMath.FormatComplex(state[1])}";
            FormulaText = $"|ψ⟩ = cos({(theta / 2).ToString("0.##", Inv)}°)|0⟩ + e^(j·{phi.ToString("0.##", Inv)}°)·sin({(theta / 2).ToString("0.##", Inv)}°)|1⟩";
            AnglesText = $"θ = {theta.ToString("0.##", Inv)}°   φ = {phi.ToString("0.##", Inv)}°";
            static double Clean(double v) => Math.Abs(v) < 5e-4 ? 0 : v; // avoid "-0.000"
            CoordinatesText = $"x = {Clean(p.X).ToString("0.000", Inv)}   y = {Clean(p.Y).ToString("0.000", Inv)}   z = {Clean(p.Z).ToString("0.000", Inv)}";
            ProbZText = $"P(0) = |α|² = {p0.ToString("0.000", Inv)}    P(1) = |β|² = {p1.ToString("0.000", Inv)}";
            ProbXText = $"X-bázis: P(+) = {pp.ToString("0.000", Inv)}    P(−) = {pm.ToString("0.000", Inv)}";
            ProbBar0 = 200 * p0;
            ProbBar1 = 200 * p1;

            foreach (var name in new[] { nameof(AlphaText), nameof(BetaText), nameof(FormulaText), nameof(AnglesText), nameof(CoordinatesText),
                                         nameof(ProbZText), nameof(ProbXText), nameof(ProbBar0), nameof(ProbBar1), nameof(ChainText) })
                OnPropertyChanged(name);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class StepAddedEventArgs : EventArgs
    {
        public StepAddedEventArgs(QubitTrack track, QubitStep step)
        {
            Track = track;
            Step = step;
        }

        public QubitTrack Track { get; }
        public QubitStep Step { get; }
    }
}
