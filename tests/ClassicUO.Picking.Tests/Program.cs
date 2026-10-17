using System;
using ClassicUO.Renderer;

var picker = new PixelPicker();
ulong id = 1;
for (var width = 1; width <= 8; width++)
{
    for (var height = 1; height <= 4; height++)
    {
        // Isolated one-pixel bones, contiguous solid runs, and row boundaries.
        for (var pattern = 0; pattern < 5; pattern++)
        {
            var pixels = new uint[width * height];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = pattern switch
                {
                    0 => 1,
                    1 => 0,
                    2 => i % 2 == 0 ? 1u : 0u,
                    3 => i % 3 == 0 ? 0u : 1u,
                    _ => i % width == width - 1 ? 1u : 0u
                };
            }
            picker.Set(id, width, height, pixels);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (picker.Get(id, x, y) != (pixels[x + y * width] != 0))
                    {
                        throw new InvalidOperationException($"Pixel mismatch: {width}x{height}, pattern={pattern}, pixel=({x},{y})");
                    }
                }
            }
            if (picker.Get(id, -1, 0) || picker.Get(id, width, 0) || picker.Get(id, 0, height))
            {
                throw new InvalidOperationException("Out-of-bounds pixel was selected");
            }
            id++;
        }
    }
}
Console.WriteLine("Pixel masks passed: single pixels, opaque/transparent spans, row edges, and bounds.");
