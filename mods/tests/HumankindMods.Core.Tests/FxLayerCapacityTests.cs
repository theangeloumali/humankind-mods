namespace HumankindMods.Core.Tests;

public class FxLayerCapacityTests
{
    [Fact]
    public void The_vanilla_4096_int_buffer_holds_headers_for_960_layers()
    {
        Assert.Equal(960, FxLayerCapacity.LayersThatFit(bufferInts: 4096, headerStart: 256, intsPerLayer: 4));
    }


    [Fact]
    public void A_buffer_smaller_than_the_header_start_fits_no_layer()
    {
        Assert.Equal(0, FxLayerCapacity.LayersThatFit(bufferInts: 100, headerStart: 256, intsPerLayer: 4));
    }
}
