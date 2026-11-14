using BlochSphereSimulator.Commands;
using BlochSphereSimulator.Models;
using MathNet.Numerics.LinearAlgebra;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Complex = System.Numerics.Complex;

namespace BlochSphereSimulator.ViewModels
{
    public class StateArrow
    {
        public Point3D Point { get; set; }
        public SolidColorBrush ColorBrush { get; set; } = Brushes.Red;
        public double Thickness { get; set; }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private Vector<Complex> _currentState;

        private double _inputTheta = 90;
        public double InputTheta
        {
            get => _inputTheta;
            set
            {
                if (_inputTheta != value)
                {
                    _inputTheta = value;
                    OnPropertyChanged();
                    InitializeState();
                }
            }
        }

        private double _inputPhi = 45;
        public double InputPhi
        {
            get => _inputPhi;
            set
            {
                if (_inputPhi != value)
                {
                    _inputPhi = value;
                    OnPropertyChanged();
                    InitializeState();
                }
            }
        }

        private double _inputRotationAngle = 45;
        public double InputRotationAngle
        {
            get => _inputRotationAngle;
            set { _inputRotationAngle = value; OnPropertyChanged(); }
        }

        private string _gateChain = "Init";
        public string GateChain
        {
            get => _gateChain;
            set { _gateChain = value; OnPropertyChanged(); }
        }

        public ObservableCollection<StateArrow> Arrows { get; set; } = new ObservableCollection<StateArrow>();

        public ICommand SetInitStateCommand { get; }
        public ICommand ApplyGateCommand { get; }
        public ICommand ClearCommand { get; }

        public MainViewModel()
        {
            _currentState = Vector<Complex>.Build.Dense(2);

            SetInitStateCommand = new RelayCommand(_ => InitializeState());
            ApplyGateCommand = new RelayCommand(ExecuteGate);
            ClearCommand = new RelayCommand(_ => InitializeState());

            InitializeState();
        }

        private void InitializeState()
        {
            _currentState = QuantumMath.GetStateVector(InputTheta, InputPhi);
            GateChain = $"Init (θ={InputTheta:F3}°, φ={InputPhi:F3}°)";
            UpdateArrows(isNewChain: true);
        }

        private void ExecuteGate(object? gateName)
        {
            if (gateName == null) return;

            double angle = InputRotationAngle;
            string gate = gateName.ToString()!;

            Matrix<Complex> matrix = gate switch
            {
                "H" => QuantumMath.H(),
                "X" => QuantumMath.X(),
                "Y" => QuantumMath.Y(),
                "Z" => QuantumMath.Z(),
                "Rx" => QuantumMath.Rx(angle),
                "Ry" => QuantumMath.Ry(angle),
                "Rz" => QuantumMath.Rz(angle),
                "P" => QuantumMath.Phase(angle), // JAVÍTÁS: Fáziskapu bekapcsolva
                _ => QuantumMath.H()
            };

            _currentState = matrix * _currentState;
            GateChain += $" ➝ {gate}";
            UpdateArrows(isNewChain: false);
        }

        private void UpdateArrows(bool isNewChain)
        {
            if (isNewChain)
            {
                Arrows.Clear();
            }
            else
            {
                foreach (var arrow in Arrows)
                {
                    arrow.ColorBrush = Brushes.LightGray;
                    arrow.Thickness = 0.02;
                }
            }

            Arrows.Add(new StateArrow
            {
                Point = QuantumMath.GetBlochCoordinates(_currentState),
                ColorBrush = Brushes.Crimson,
                Thickness = 0.08
            });

            var temp = Arrows;
            Arrows = new ObservableCollection<StateArrow>();
            foreach (var item in temp) Arrows.Add(item);
            OnPropertyChanged(nameof(Arrows));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}