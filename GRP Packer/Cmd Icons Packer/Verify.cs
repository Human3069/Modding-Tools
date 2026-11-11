using System;
using System.IO;
using System.Drawing;

internal class Verify
{
    private static int Main()
    {
        int frameCount = 0;
        {
            string directoryPath = Path.Combine("..", "origins", "unit", "cmdbtns");
            string ralPath = Directory.GetFiles(directoryPath, "*.ral")[0];
            string[] ralLines = File.ReadAllLines(ralPath);
            var frameList = Packer.Decode(File.ReadAllBytes(Path.Combine("output", ralLines[0])));
            byte[] paletteBytes = File.ReadAllBytes(Path.Combine(directoryPath, ralLines[1]));
            int paletteStart = 14 + BitConverter.ToInt32(paletteBytes, 14);
            for (int frameIndex = 0; frameIndex < frameList.Count; frameIndex++)
            {
                using (var bitmapImage = new Bitmap(Path.Combine(directoryPath, ralLines[frameIndex + 1])))
                {
                    var frameData = frameList[frameIndex];
                    for (int y = 0; y < frameData.Height; y++)
                    {
                        for (int x = 0; x < frameData.Width; x++)
                        {
                            int paletteOffset = paletteStart + 4 * frameData.Pixels[y * frameData.Width + x];
                            Color expectedColor = Color.FromArgb(paletteBytes[paletteOffset + 2], paletteBytes[paletteOffset + 1], paletteBytes[paletteOffset]);
                            if (bitmapImage.GetPixel(x, y).ToArgb() != expectedColor.ToArgb())
                            {
                                throw new Exception(ralLines[frameIndex + 1] + " RGB mismatch at " + x + "," + y);
                            }
                        }
                    }

                    frameCount++;
                }
            }
        }

        Console.WriteLine("Independent System.Drawing RGB comparison passed: " + frameCount + " frames (normalized canvas).");
        return 0;
    }
}


