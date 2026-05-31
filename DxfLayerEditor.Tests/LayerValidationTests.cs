using System.Linq;
using DxfLayerEditor.Models;
using Xunit;

namespace DxfLayerEditor.Tests
{
    /// <summary>
    /// Unit tests for Thermwood layer name validation.
    /// </summary>
    public class LayerValidationTests
    {
        [Theory]
        [InlineData("outline")]
        [InlineData("border")]
        [InlineData("panel")]
        [InlineData("cutout")]
        [InlineData("pocket")]
        [InlineData("pocketclamp")]
        [InlineData("mortise")]
        [InlineData("drill")]
        [InlineData("vgroove")]
        [InlineData("score")]
        [InlineData("dado")]
        public void ValidateName_ValidTypes_NoErrors(string name)
        {
            var errors = Layer.ValidateName(name);
            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("outline_router")]
        [InlineData("pocket2")]
        [InlineData("drill_8mm")]
        public void ValidateName_ValidTypesWithSuffix_NoErrors(string name)
        {
            var errors = Layer.ValidateName(name);
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateName_BackOutline_Forbidden()
        {
            var errors = Layer.ValidateName("backoutline");
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("back"));
        }

        [Fact]
        public void ValidateName_BackBorder_Forbidden()
        {
            var errors = Layer.ValidateName("back_border");
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void ValidateName_BackPanel_Forbidden()
        {
            var errors = Layer.ValidateName("back_panel");
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void ValidateName_BackPocket_Allowed()
        {
            var errors = Layer.ValidateName("backpocket");
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateName_BackDrill_Allowed()
        {
            var errors = Layer.ValidateName("backdrill");
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateName_Empty_HasError()
        {
            var errors = Layer.ValidateName("");
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void ValidateName_InvalidType_HasError()
        {
            var errors = Layer.ValidateName("foobar");
            Assert.NotEmpty(errors);
        }

        [Fact]
        public void IsOutlineType_Outline_True()
        {
            Assert.True(Layer.IsOutlineType("outline"));
            Assert.True(Layer.IsOutlineType("border"));
            Assert.True(Layer.IsOutlineType("panel"));
        }

        [Fact]
        public void IsOutlineType_Pocket_False()
        {
            Assert.False(Layer.IsOutlineType("pocket"));
            Assert.False(Layer.IsOutlineType("drill"));
        }

        [Fact]
        public void ShouldAutoClose_OutlineBorderPanel_True()
        {
            Assert.True(Layer.ShouldAutoClose("outline"));
            Assert.True(Layer.ShouldAutoClose("border"));
            Assert.True(Layer.ShouldAutoClose("panel"));
            Assert.True(Layer.ShouldAutoClose("pocket"));
            Assert.True(Layer.ShouldAutoClose("pocketclamp"));
            Assert.True(Layer.ShouldAutoClose("dado"));
        }

        [Fact]
        public void ShouldAutoClose_Drill_False()
        {
            Assert.False(Layer.ShouldAutoClose("drill"));
            Assert.False(Layer.ShouldAutoClose("vgroove"));
            Assert.False(Layer.ShouldAutoClose("score"));
        }
    }
}
