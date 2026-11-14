using BlochSphereSimulator.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace BlochSphereSimulator.Tests
{
    [TestClass]
    public class MainViewModelTests
    {
        [TestMethod]
        public void Constructor_InitializesOneArrow()
        {
            var vm = new MainViewModel();

            Assert.IsNotNull(vm.Arrows);
            Assert.AreEqual(1, vm.Arrows.Count);
            Assert.IsTrue(vm.GateChain.StartsWith("Init"));
        }

        [TestMethod]
        public void ApplyGateCommand_AddsArrow_AndExtendsChain()
        {
            var vm = new MainViewModel();
            var before = vm.Arrows.Count;

            vm.ApplyGateCommand.Execute("X");

            Assert.AreEqual(before + 1, vm.Arrows.Count);
            Assert.IsTrue(vm.GateChain.Contains("X"));
            Assert.IsTrue(vm.Arrows.Last().Thickness >= 0.08);
        }

        [TestMethod]
        public void ClearCommand_ResetsToSingleArrow_AndInitChain()
        {
            var vm = new MainViewModel();
            vm.ApplyGateCommand.Execute("H");
            vm.ApplyGateCommand.Execute("Z");

            vm.ClearCommand.Execute(null);

            Assert.AreEqual(1, vm.Arrows.Count);
            Assert.IsTrue(vm.GateChain.StartsWith("Init"));
        }
    }
}