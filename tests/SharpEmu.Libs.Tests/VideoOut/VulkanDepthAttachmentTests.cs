// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.Libs.Agc;
using SharpEmu.Libs.Gpu;
using SharpEmu.Libs.VideoOut;
using Silk.NET.Vulkan;
using Xunit;

namespace SharpEmu.Libs.Tests.VideoOut;

public sealed class VulkanDepthAttachmentTests
{
    private static readonly GuestDepthTarget Target = new(
        ReadAddress: 0x1000,
        WriteAddress: 0x1000,
        Width: 1920,
        Height: 1080,
        GuestFormat: 1,
        SwizzleMode: 0,
        ClearDepth: 1f,
        ReadOnly: false);

    [Fact]
    public void Z16DepthTarget_UsesD16Unorm()
    {
        Assert.Equal(Format.D16Unorm, VulkanVideoPresenter.GetDepthFormat(1));
    }

    [Fact]
    public void Z32FloatDepthTarget_UsesD32Sfloat()
    {
        Assert.Equal(Format.D32Sfloat, VulkanVideoPresenter.GetDepthFormat(3));
    }

    [Theory]
    [InlineData(1u, SampleCountFlags.Count1Bit)]
    [InlineData(2u, SampleCountFlags.Count2Bit)]
    [InlineData(4u, SampleCountFlags.Count4Bit)]
    [InlineData(8u, SampleCountFlags.Count8Bit)]
    public void Ps5SampleCountMapsToNativeVulkanFlag(
        uint samples,
        SampleCountFlags expected)
    {
        Assert.True(VulkanVideoPresenter.TryGetVulkanSampleCount(samples, out var actual));
        Assert.Equal(expected, actual);
        Assert.False(VulkanVideoPresenter.TryGetVulkanSampleCount(3, out _));
    }

    [Fact]
    public void Z16DepthTarget_ExactR16StorageDescriptorIsAnAlias()
    {
        var depth = Target with { SwizzleMode = 24 };
        var storage = new GuestDrawTexture(
            Address: depth.Address,
            Width: depth.Width,
            Height: depth.Height,
            Format: 2,
            NumberType: 0,
            RgbaPixels: [],
            IsFallback: false,
            IsStorage: true,
            TileMode: 24,
            Pitch: depth.Width,
            ArrayedView: true);

        Assert.True(VulkanVideoPresenter.IsD16DepthStorageAlias(depth, storage));
        Assert.False(VulkanVideoPresenter.IsD16DepthStorageAlias(
            depth,
            storage with { Width = depth.Width - 1 }));
        Assert.False(VulkanVideoPresenter.IsD16DepthStorageAlias(
            depth,
            storage with { TileMode = 0 }));
        Assert.False(VulkanVideoPresenter.IsD16DepthStorageAlias(
            depth with { Samples = 4 },
            storage));
        Assert.False(VulkanVideoPresenter.IsD16DepthStorageAlias(
            depth with { StencilAccess = true, StencilSize = 0x10000 },
            storage));
    }

    [Theory]
    [InlineData(1u, 2u, 0u)]
    [InlineData(3u, 4u, 7u)]
    public void DepthTiledTypedSampledDescriptorIsAnAlias(
        uint depthFormat,
        uint textureFormat,
        uint numberType)
    {
        var depth = Target with { GuestFormat = depthFormat, SwizzleMode = 24 };
        var texture = new GuestDrawTexture(
            Address: depth.Address,
            Width: depth.Width,
            Height: depth.Height,
            Format: textureFormat,
            NumberType: numberType,
            RgbaPixels: [],
            IsFallback: false,
            IsStorage: false,
            TileMode: 24,
            DstSelect: 0x924);

        Assert.True(VulkanVideoPresenter.IsSupportedSampledDepthAlias(depth, texture));
        Assert.True(VulkanVideoPresenter.IsSupportedSampledDepthAlias(
            depth,
            texture with { ArrayedView = true, ArrayLayers = 1 }));
        Assert.False(VulkanVideoPresenter.IsSupportedSampledDepthAlias(
            depth with { Samples = 4 },
            texture));
    }

    [Fact]
    public void DepthAliasRejectsColourOrUnsupportedDepthDescriptor()
    {
        var depth = Target with { SwizzleMode = 24 };
        var texture = new GuestDrawTexture(
            Address: depth.Address,
            Width: depth.Width,
            Height: depth.Height,
            Format: 2,
            NumberType: 0,
            RgbaPixels: [],
            IsFallback: false,
            IsStorage: false,
            TileMode: 24,
            DstSelect: 0x924);

        Assert.False(VulkanVideoPresenter.IsSupportedSampledDepthAlias(
            depth,
            texture with { TileMode = 0 }));
        Assert.False(VulkanVideoPresenter.IsSupportedSampledDepthAlias(
            depth,
            texture with { DstSelect = 0xFAC }));
        Assert.False(VulkanVideoPresenter.IsSupportedSampledDepthAlias(
            depth,
            texture with { Format = 1 }));
        Assert.False(VulkanVideoPresenter.IsSupportedSampledDepthAlias(
            depth,
            texture with { ArrayLayers = 2 }));
    }

    [Fact]
    public void GuestDepthTarget_AttachesForDepthWork()
    {
        var state = new GuestDepthState(
            TestEnable: true,
            WriteEnable: true,
            CompareOp: 3);

        Assert.True(VulkanVideoPresenter.ShouldAttachGuestDepth(Target, state));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void GuestDepthTarget_AttachesForEitherDepthOperation(
        bool testEnable,
        bool writeEnable)
    {
        var state = new GuestDepthState(testEnable, writeEnable, CompareOp: 3);

        Assert.True(VulkanVideoPresenter.ShouldAttachGuestDepth(Target, state));
    }

    [Fact]
    public void GuestDepthTarget_RequiresTargetAndDepthWork()
    {
        var state = GuestDepthState.Default;

        Assert.False(VulkanVideoPresenter.ShouldAttachGuestDepth(Target, state));
        Assert.False(VulkanVideoPresenter.ShouldAttachGuestDepth(
            target: null,
            new GuestDepthState(true, false, CompareOp: 3)));
    }

    [Fact]
    public void GuestDepthTarget_AttachesForDepthClear()
    {
        var state = new GuestDepthState(
            TestEnable: false,
            WriteEnable: false,
            CompareOp: 7,
            ClearEnable: true);

        Assert.True(VulkanVideoPresenter.ShouldAttachGuestDepth(Target, state));
    }

    [Fact]
    public void RasterState_DecodesFrontPolygonDepthBias()
    {
        var registers = new Dictionary<uint, uint>
        {
            [0x205] = 1u << 11,
            [0x2DF] = BitConverter.SingleToUInt32Bits(0.25f),
            [0x2E0] = BitConverter.SingleToUInt32Bits(2.5f),
            [0x2E1] = BitConverter.SingleToUInt32Bits(-1.5f),
        };

        var raster = AgcExports.DecodeRasterState(registers);

        Assert.True(raster.DepthBiasEnable);
        Assert.Equal(-1.5f, raster.DepthBiasConstantFactor);
        Assert.Equal(0.25f, raster.DepthBiasClamp);
        Assert.Equal(2.5f, raster.DepthBiasSlopeFactor);
    }

    [Fact]
    public void RasterState_UsesBackPolygonDepthBiasWhenOnlyBackIsEnabled()
    {
        var registers = new Dictionary<uint, uint>
        {
            [0x205] = 1u << 12,
            [0x2DF] = BitConverter.SingleToUInt32Bits(0.5f),
            [0x2E2] = BitConverter.SingleToUInt32Bits(3f),
            [0x2E3] = BitConverter.SingleToUInt32Bits(4f),
        };

        var raster = AgcExports.DecodeRasterState(registers);

        Assert.True(raster.DepthBiasEnable);
        Assert.Equal(4f, raster.DepthBiasConstantFactor);
        Assert.Equal(0.5f, raster.DepthBiasClamp);
        Assert.Equal(3f, raster.DepthBiasSlopeFactor);
    }

    [Fact]
    public void ClipControl_DisablesDepthClipOnlyWhenBothPlanesAreDisabled()
    {
        Assert.False(AgcExports.DecodeDepthClipEnabled(new Dictionary<uint, uint>
        {
            [0x204] = (1u << 26) | (1u << 27),
        }));
        Assert.True(AgcExports.DecodeDepthClipEnabled(new Dictionary<uint, uint>
        {
            [0x204] = 1u << 26,
        }));
        Assert.True(AgcExports.DecodeDepthClipEnabled(new Dictionary<uint, uint>()));
    }

    [Theory]
    [InlineData(0x41u, true)]
    [InlineData(0x40u, false)]
    public void DepthState_DecodesRenderControlClearBit(
        uint renderControl,
        bool clearEnable)
    {
        var registers = new Dictionary<uint, uint>
        {
            [0x000] = renderControl,
            [0x200] = 0x776,
        };

        var state = AgcExports.DecodeDepthState(registers);

        Assert.True(state.TestEnable);
        Assert.True(state.WriteEnable);
        Assert.Equal(7u, state.CompareOp);
        Assert.Equal(clearEnable, state.ClearEnable);
    }
}
