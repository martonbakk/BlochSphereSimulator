using MathNet.Numerics.LinearAlgebra;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Complex = System.Numerics.Complex;

namespace BlochSphereSimulator.Models
{
    public enum StepKind { Init, Gate, Measurement }

    /// <summary>One entry of a qubit's history: the state after a gate/measurement and the path that led there.</summary>
    public sealed class QubitStep
    {
        public QubitStep(StepKind kind, string label, Vector<Complex> state, IReadOnlyList<Point3D> path, Vector3D? axis = null)
        {
            Kind = kind;
            Label = label;
            State = state;
            Bloch = QuantumMath.GetBlochCoordinates(state);
            Path = path;
            Axis = axis;
        }

        /// <summary>Rotation axis of the gate on the Bloch sphere (null for init and measurement).</summary>
        public Vector3D? Axis { get; }

        public StepKind Kind { get; }
        public string Label { get; }
        public Vector<Complex> State { get; }
        public Point3D Bloch { get; }

        /// <summary>Points traced from the previous Bloch vector to this one (empty for the initial state).</summary>
        public IReadOnlyList<Point3D> Path { get; }
    }

    /// <summary>An independent qubit shown on the shared Bloch sphere, with its own colour and gate chain.</summary>
    public sealed class QubitTrack : INotifyPropertyChanged
    {
        public QubitTrack(string name, Color color, Vector<Complex> initialState, string initLabel)
        {
            Name = name;
            Color = color;
            Brush = new SolidColorBrush(color);
            Brush.Freeze();
            Steps.Add(new QubitStep(StepKind.Init, initLabel, initialState, new List<Point3D>()));
        }

        public string Name { get; }
        public Color Color { get; }
        public SolidColorBrush Brush { get; }

        public ObservableCollection<QubitStep> Steps { get; } = new();

        public QubitStep Current => Steps[^1];

        public bool CanUndo => Steps.Count > 1;

        public override string ToString() => Name;

        /// <summary>Gate chain text, e.g. "Init(|0⟩) ➝ H ➝ P(45°) ➝ M_Z=0".</summary>
        public string ChainText => string.Join(" ➝ ", Steps.Select(s => s.Label));

        public void Push(QubitStep step)
        {
            Steps.Add(step);
            RaiseAll();
        }

        public bool Undo()
        {
            if (!CanUndo) return false;
            Steps.RemoveAt(Steps.Count - 1);
            RaiseAll();
            return true;
        }

        public void Restart(Vector<Complex> initialState, string initLabel)
        {
            Steps.Clear();
            Steps.Add(new QubitStep(StepKind.Init, initLabel, initialState, new List<Point3D>()));
            RaiseAll();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(Current));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(ChainText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
