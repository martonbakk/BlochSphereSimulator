using BlochSphereSimulator.Models;
using BlochSphereSimulator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BlochSphereSimulator.Tests
{
    [TestClass]
    public class MainViewModelTests
    {
        private static MainViewModel NewVm() => new(new Random(5));

        [TestMethod]
        public void Constructor_CreatesOneQubitInKet0()
        {
            var vm = NewVm();
            Assert.AreEqual(1, vm.Qubits.Count);
            Assert.AreSame(vm.Qubits[0], vm.SelectedQubit);
            Assert.AreEqual(1, vm.SelectedQubit!.Steps.Count);
            Assert.AreEqual(1.0, vm.SelectedQubit.Current.Bloch.Z, 1e-12);
            StringAssert.StartsWith(vm.SelectedQubit.ChainText, "Init");
        }

        [TestMethod]
        public void ApplyGate_AddsStep_ExtendsChain_AndRaisesStepAdded()
        {
            var vm = NewVm();
            StepAddedEventArgs? raised = null;
            vm.StepAdded += (_, e) => raised = e;

            vm.ApplyGateCommand.Execute("H");

            Assert.AreEqual(2, vm.SelectedQubit!.Steps.Count);
            StringAssert.Contains(vm.SelectedQubit.ChainText, "➝ H");
            Assert.IsNotNull(raised);
            Assert.AreEqual(StepKind.Gate, raised!.Step.Kind);
            Assert.IsTrue(raised.Step.Path.Count > 2);
            Assert.AreEqual(1.0, vm.SelectedQubit.Current.Bloch.X, 1e-9);
            StringAssert.Contains(vm.ProbZText, "0.500");
        }

        [TestMethod]
        public void ParametricGate_UsesRotationAngleInput()
        {
            var vm = NewVm();
            vm.InputRotationAngle = 90;
            vm.ApplyGateCommand.Execute("Ry");
            Assert.AreEqual("Ry(90°)", vm.SelectedQubit!.Current.Label);
            Assert.AreEqual(1.0, vm.SelectedQubit.Current.Bloch.X, 1e-9);
        }

        [TestMethod]
        public void Undo_RemovesLastStep()
        {
            var vm = NewVm();
            vm.ApplyGateCommand.Execute("H");
            vm.ApplyGateCommand.Execute("S");
            vm.UndoCommand.Execute(null);
            Assert.AreEqual(2, vm.SelectedQubit!.Steps.Count);
            Assert.AreEqual("H", vm.SelectedQubit.Current.Label);
            vm.UndoCommand.Execute(null);
            vm.UndoCommand.Execute(null); // nothing left to undo: no exception, init stays
            Assert.AreEqual(1, vm.SelectedQubit.Steps.Count);
        }

        [TestMethod]
        public void Clear_ReturnsToInitialState()
        {
            var vm = NewVm();
            vm.ApplyPresetForTest("|+⟩");
            vm.ApplyGateCommand.Execute("Z");
            vm.ClearCommand.Execute(null);
            Assert.AreEqual(1, vm.SelectedQubit!.Steps.Count);
            Assert.AreEqual(1.0, vm.SelectedQubit.Current.Bloch.X, 1e-9);
        }

        [TestMethod]
        public void Sliders_UpdateInitLive_ButDoNotWipeAnExistingChain()
        {
            var vm = NewVm();
            vm.InputTheta = 90; // only init exists -> live update
            Assert.AreEqual(1.0, vm.SelectedQubit!.Current.Bloch.X, 1e-9);

            vm.ApplyGateCommand.Execute("X");
            vm.InputPhi = 90; // chain exists -> kept, hint shown
            Assert.AreEqual(2, vm.SelectedQubit.Steps.Count);
            Assert.AreNotEqual("", vm.InputHint);

            vm.SetInitStateCommand.Execute(null); // explicit restart
            Assert.AreEqual(1, vm.SelectedQubit.Steps.Count);
            Assert.AreEqual(1.0, vm.SelectedQubit.Current.Bloch.Y, 1e-9);
            Assert.AreEqual("", vm.InputHint);
        }

        [TestMethod]
        public void Inputs_AreClamped()
        {
            var vm = NewVm();
            vm.InputTheta = 500;
            vm.InputPhi = -20;
            Assert.AreEqual(180, vm.InputTheta);
            Assert.AreEqual(0, vm.InputPhi);
        }

        [TestMethod]
        public void Presets_SetInputsAndRestart()
        {
            var vm = NewVm();
            vm.ApplyGateCommand.Execute("H");
            vm.PresetCommand.Execute("|−j⟩");
            Assert.AreEqual(90, vm.InputTheta);
            Assert.AreEqual(270, vm.InputPhi);
            Assert.AreEqual(1, vm.SelectedQubit!.Steps.Count);
            Assert.AreEqual(-1.0, vm.SelectedQubit.Current.Bloch.Y, 1e-9);
        }

        [TestMethod]
        public void Measure_Z_CollapsesAndRecordsStep()
        {
            var vm = NewVm();
            vm.ApplyGateCommand.Execute("H");
            vm.MeasureCommand.Execute("Z");
            var step = vm.SelectedQubit!.Current;
            Assert.AreEqual(StepKind.Measurement, step.Kind);
            StringAssert.StartsWith(step.Label, "M_Z=");
            Assert.AreEqual(1.0, Math.Abs(step.Bloch.Z), 1e-9);
            StringAssert.Contains(vm.LastMeasurementText, "50.0");
        }

        [TestMethod]
        public void Measure_X_OnPlus_AlwaysGivesPlus()
        {
            var vm = NewVm();
            vm.ApplyPresetForTest("|+⟩");
            for (int i = 0; i < 10; i++) vm.MeasureCommand.Execute("X");
            Assert.IsTrue(vm.SelectedQubit!.Steps.Skip(1).All(s => s.Label == "M_X=+"));
        }

        [TestMethod]
        public void RunShots_FillsHistogram_WithoutChangingState()
        {
            var vm = NewVm();
            vm.InputTheta = 60; // P(0) = 0.75
            vm.ShotCount = 10000;
            vm.RunShotsCommand.Execute(null);

            Assert.AreEqual(1, vm.SelectedQubit!.Steps.Count);
            Assert.AreEqual(10000, vm.LastShots.Count0 + vm.LastShots.Count1);
            Assert.AreEqual(0.75, vm.LastShots.Count0 / 10000.0, 0.02);
            Assert.IsTrue(vm.HistogramBar0 > vm.HistogramBar1);

            vm.ApplyGateCommand.Execute("X"); // state changed -> stale histogram is cleared
            Assert.AreEqual(0, vm.HistogramBar0);
        }

        [TestMethod]
        public void MultipleQubits_AreIndependent()
        {
            var vm = NewVm();
            var q0 = vm.SelectedQubit!;
            vm.AddQubitCommand.Execute(null);
            var q1 = vm.SelectedQubit!;
            Assert.AreNotSame(q0, q1);
            Assert.AreNotEqual(q0.Color, q1.Color);

            vm.ApplyGateCommand.Execute("X");
            Assert.AreEqual(1, q0.Steps.Count);
            Assert.AreEqual(2, q1.Steps.Count);

            vm.SelectedQubit = q0;
            vm.ApplyGateCommand.Execute("H");
            Assert.AreEqual(-1.0, q1.Current.Bloch.Z, 1e-9);
            Assert.AreEqual(1.0, q0.Current.Bloch.X, 1e-9);

            vm.RemoveQubitCommand.Execute(null);
            Assert.AreEqual(1, vm.Qubits.Count);
            Assert.AreSame(q1, vm.SelectedQubit);
        }

        [TestMethod]
        public void MaxQubits_IsEnforced()
        {
            var vm = NewVm();
            for (int i = 0; i < 10; i++) vm.AddQubitCommand.Execute(null);
            Assert.AreEqual(MainViewModel.MaxQubits, vm.Qubits.Count);
        }

        [TestMethod]
        public async Task PreparationDemo_ReachesTarget()
        {
            var vm = NewVm();
            vm.InputTheta = 60;
            vm.InputPhi = 120;
            await vm.RunPreparationDemoAsync(0);

            var labels = vm.SelectedQubit!.Steps.Skip(1).Select(s => s.Label).ToArray();
            CollectionAssert.AreEqual(new[] { "H", "P(60°)", "H", "P(210°)" }, labels);
            var expected = QuantumMath.GetBlochCoordinates(QuantumMath.GetStateVector(60, 120));
            var actual = vm.SelectedQubit.Current.Bloch;
            Assert.AreEqual(expected.X, actual.X, 1e-9);
            Assert.AreEqual(expected.Y, actual.Y, 1e-9);
            Assert.AreEqual(expected.Z, actual.Z, 1e-9);
            Assert.IsFalse(vm.IsBusy);
        }
    }

    internal static class MainViewModelTestExtensions
    {
        public static void ApplyPresetForTest(this MainViewModel vm, string name) => vm.PresetCommand.Execute(name);
    }
}
