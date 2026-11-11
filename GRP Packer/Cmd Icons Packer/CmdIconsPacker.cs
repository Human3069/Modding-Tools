using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Drawing;

internal class ImageData
{
    private int _width;
    private int _height;
    private byte[] _pixelBytes;

    public int Width
    {
        get
        {
            return _width;
        }

        set
        {
            _width = value;
        }
    }

    public int Height
    {
        get
        {
            return _height;
        }

        set
        {
            _height = value;
        }
    }

    public byte[] Pixels
    {
        get
        {
            return _pixelBytes;
        }

        set
        {
            _pixelBytes = value;
        }
    }
}

internal class Packer
{
    private static int rgbFallbackCount;
    private static void Need(bool isValid, string messageText)
    {
        if (isValid == false)
        {
            throw new InvalidDataException(messageText);
        }
    }

    public static ImageData ReadBmp(string filePath)
    {
        byte[] dataBytes = File.ReadAllBytes(filePath);
        Need(dataBytes.Length >= 54 && dataBytes[0] == 66 && dataBytes[1] == 77, "Invalid BMP: " + filePath);
        int pixelDataOffset = BitConverter.ToInt32(dataBytes, 10);
        int dibHeaderSize = BitConverter.ToInt32(dataBytes, 14);
        int canvasWidth = BitConverter.ToInt32(dataBytes, 18);
        int signedHeight = BitConverter.ToInt32(dataBytes, 22);
        int bitDepth = BitConverter.ToUInt16(dataBytes, 28);
        int compression = BitConverter.ToInt32(dataBytes, 30);
        Need(dibHeaderSize >= 40 && canvasWidth > 0 && canvasWidth <= 255 && signedHeight != 0 && signedHeight >= -255 && signedHeight <= 255, "BMP dimensions must be 1..255: " + filePath);
        if ((bitDepth == 24 || bitDepth == 32) && compression == 0)
        {
            string folderPath = Path.GetDirectoryName(filePath);
            string referencePath = Directory.GetFiles(folderPath, "*.bmp").FirstOrDefault(delegate (string candidatePath)
            {
                byte[] candidateBytes = File.ReadAllBytes(candidatePath);
                return candidateBytes.Length > 54 && BitConverter.ToUInt16(candidateBytes, 28) == 8;
            });
            Need(referencePath != null, "RGB BMP needs an indexed BMP palette in the same folder");
            byte[] paletteBytes = File.ReadAllBytes(referencePath);
            int paletteStart = 14 + BitConverter.ToInt32(paletteBytes, 14);
            int colorCount = BitConverter.ToInt32(paletteBytes, 46);
            if (colorCount == 0)
            {
                colorCount = 256;
            }

            Need(colorCount <= 256 && paletteStart + colorCount * 4 <= paletteBytes.Length, "Invalid reference palette");
            var paletteLookup = new Dictionary<int, List<byte>>();
            for (int frameIndex = 0; frameIndex < colorCount; frameIndex++)
            {
                int commandValue = paletteBytes[paletteStart + 4 * frameIndex] | paletteBytes[paletteStart + 4 * frameIndex + 1] << 8 | paletteBytes[paletteStart + 4 * frameIndex + 2] << 16;
                if (paletteLookup.ContainsKey(commandValue) == false)
                {
                    paletteLookup[commandValue] = new List<byte>();
                }

                paletteLookup[commandValue].Add((byte)frameIndex);
            }

            ImageData originalImageData = null;
            string fileStem = Path.GetFileNameWithoutExtension(filePath);
            Match frameMatch = Regex.Match(fileStem, @"^(.*?)(\d+)$");
            if (frameMatch.Success == true)
            {
                string originalGrpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "origins", "unit", "cmdbtns", frameMatch.Groups[1].Value + ".grp");
                if (File.Exists(originalGrpPath) == true)
                {
                    var frameList = Decode(File.ReadAllBytes(originalGrpPath));
                    int frameIndex = int.Parse(frameMatch.Groups[2].Value);
                    if (frameIndex < frameList.Count && frameList[frameIndex].Width == canvasWidth && frameList[frameIndex].Height == Math.Abs(signedHeight))
                    {
                        originalImageData = frameList[frameIndex];
                    }
                }
            }

            int imageHeight = Math.Abs(signedHeight);
            int rowStride = ((canvasWidth * (bitDepth / 8) + 3) & ~3);
            Need((long)pixelDataOffset + (long)rowStride * imageHeight <= dataBytes.Length, "Truncated RGB BMP");
            byte[] rgbPixelBytes = new byte[canvasWidth * imageHeight];
            for (int rowIndex = 0; rowIndex < imageHeight; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < canvasWidth; columnIndex++)
                {
                    int pixelOffset = pixelDataOffset + rowIndex * rowStride + columnIndex * (bitDepth / 8);
                    int commandValue = dataBytes[pixelOffset] | dataBytes[pixelOffset + 1] << 8 | dataBytes[pixelOffset + 2] << 16;
                    List<byte> paletteIndices;
                    Need(paletteLookup.TryGetValue(commandValue, out paletteIndices), "RGB color is absent from indexed palette: " + filePath + " x=" + columnIndex + " y=" + rowIndex);
                    int destinationOffset = (signedHeight > 0 ? imageHeight - 1 - rowIndex : rowIndex) * canvasWidth + columnIndex;
                    byte chosenIndex = paletteIndices[0];
                    if (paletteIndices.Count > 1)
                    {
                        if (originalImageData != null && paletteIndices.Contains(originalImageData.Pixels[destinationOffset]) == true)
                        {
                            chosenIndex = originalImageData.Pixels[destinationOffset];
                        }
                        else
                        {
                            // RGB exports lose palette-index identity. Use the lowest exact-match index.
                            chosenIndex = paletteIndices[0];
                            rgbFallbackCount++;
                        }
                    }

                    rgbPixelBytes[destinationOffset] = chosenIndex;
                }
            }

            return new ImageData
            {
                Width = canvasWidth,
                Height = imageHeight,
                Pixels = rgbPixelBytes
            };
        }

        Need(bitDepth == 8 && (compression == 0 || compression == 1), "Use indexed 8-bit BMP (BI_RGB or RLE8). RGB conversion loses palette indices: " + filePath);
        int canvasHeight = Math.Abs(signedHeight);
        byte[] pixelBytes = new byte[canvasWidth * canvasHeight];
        Need(pixelDataOffset >= 14 + dibHeaderSize && pixelDataOffset <= dataBytes.Length, "Invalid BMP data offset");
        if (compression == 0)
        {
            int rowStride = (canvasWidth + 3) & ~3;
            Need((long)pixelDataOffset + (long)rowStride * canvasHeight <= dataBytes.Length, "Truncated BMP");
            for (int y = 0; y < canvasHeight; y++)
            {
                Buffer.BlockCopy(dataBytes, pixelDataOffset + y * rowStride, pixelBytes, (signedHeight > 0 ? canvasHeight - 1 - y : y) * canvasWidth, canvasWidth);
            }
        }
        else
        {
            Need(signedHeight > 0, "Top-down RLE8 is invalid");
            int dataPosition = pixelDataOffset;
            int x = 0;
            int y = 0;
            bool isEndMarkerFound = false;
            while (dataPosition < dataBytes.Length)
            {
                Need(dataPosition + 2 <= dataBytes.Length, "Truncated RLE8 command");
                int runLength = dataBytes[dataPosition++];
                int pixelValue = dataBytes[dataPosition++];
                if (runLength > 0)
                {
                    Need(y < canvasHeight, "RLE8 row out of bounds");
                    for (int runIndex = 0; runIndex < runLength; runIndex++)
                    {
                        if (x < canvasWidth)
                        {
                            pixelBytes[(canvasHeight - 1 - y) * canvasWidth + x] = (byte)pixelValue;
                        }

                        x++;
                    }
                }
                else if (pixelValue == 0)
                {
                    x = 0;
                    y++;
                    Need(y <= canvasHeight, "RLE8 row out of bounds");
                }
                else if (pixelValue == 1)
                {
                    isEndMarkerFound = true;
                    break;
                }
                else if (pixelValue == 2)
                {
                    Need(dataPosition + 2 <= dataBytes.Length, "Truncated RLE8 delta");
                    x += dataBytes[dataPosition++];
                    y += dataBytes[dataPosition++];
                    Need(x <= canvasWidth && y < canvasHeight, "RLE8 delta out of bounds");
                }
                else
                {
                    Need(dataPosition + pixelValue + (pixelValue & 1) <= dataBytes.Length && y < canvasHeight, "Invalid RLE8 literal");
                    for (int runIndex = 0; runIndex < pixelValue; runIndex++)
                    {
                        if (x < canvasWidth)
                        {
                            pixelBytes[(canvasHeight - 1 - y) * canvasWidth + x] = dataBytes[dataPosition];
                        }

                        x++;
                        dataPosition++;
                    }

                    dataPosition += pixelValue & 1;
                }
            }

            Need(isEndMarkerFound, "Missing RLE8 end marker");
        }

        return new ImageData
        {
            Width = canvasWidth,
            Height = canvasHeight,
            Pixels = pixelBytes
        };
    }

    private static byte[] Encode(ImageData imageData, out byte left, out byte top, out byte width, out byte height)
    {
        int leftBound = imageData.Width;
        int topBound = imageData.Height;
        int rightBound = -1;
        int bottomBound = -1;
        for (int y = 0; y < imageData.Height; y++)
        {
            for (int x = 0; x < imageData.Width; x++)
            {
                if (imageData.Pixels[y * imageData.Width + x] != 0)
                {
                    leftBound = Math.Min(leftBound, x);
                    rightBound = Math.Max(rightBound, x);
                    topBound = Math.Min(topBound, y);
                    bottomBound = Math.Max(bottomBound, y);
                }
            }
        }

        if (rightBound < 0)
        {
            leftBound = topBound = 0;
            rightBound = bottomBound = 0;
        } // A transparent 1x1 frame is safe for the classic renderer.

        left = (byte)leftBound;
        top = (byte)topBound;
        width = (byte)(rightBound - leftBound + 1);
        height = (byte)(bottomBound - topBound + 1);
        using (var memoryStream = new MemoryStream())
        {
            using (var binaryWriter = new BinaryWriter(memoryStream))
            {
                binaryWriter.Write(new byte[height * 2]);
                for (int y = topBound; y <= bottomBound; y++)
                {
                    long lineOffset = memoryStream.Position;
                    Need(lineOffset <= 65535, "GRP scanline offset exceeds 16-bit limit");
                    memoryStream.Position = (y - topBound) * 2;
                    binaryWriter.Write((ushort)lineOffset);
                    memoryStream.Position = lineOffset;
                    int x = leftBound;
                    while (x <= rightBound)
                    {
                        byte pixelValue = imageData.Pixels[y * imageData.Width + x];
                        int runLength = 1;
                        if (pixelValue == 0)
                        {
                            while (x + runLength <= rightBound && runLength < 127 && imageData.Pixels[y * imageData.Width + x + runLength] == 0)
                            {
                                runLength++;
                            }

                            binaryWriter.Write((byte)(128 | runLength));
                        }
                        else
                        {
                            while (x + runLength <= rightBound && runLength < 63 && imageData.Pixels[y * imageData.Width + x + runLength] == pixelValue)
                            {
                                runLength++;
                            }

                            binaryWriter.Write((byte)(64 | runLength));
                            binaryWriter.Write(pixelValue);
                        }

                        x += runLength;
                    }
                }

                return memoryStream.ToArray();
            }
        }
    }

    public static List<ImageData> Decode(byte[] dataBytes)
    {
        Need(dataBytes.Length >= 6, "Short GRP");
        int frameCount = BitConverter.ToUInt16(dataBytes, 0);
        int canvasWidth = BitConverter.ToUInt16(dataBytes, 2);
        int canvasHeight = BitConverter.ToUInt16(dataBytes, 4);
        Need(frameCount > 0 && canvasWidth > 0 && canvasHeight > 0 && dataBytes.Length >= 6 + 8 * frameCount, "Invalid GRP header");
        var frameList = new List<ImageData>();
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            int headerOffset = 6 + 8 * frameIndex;
            int leftBound = dataBytes[headerOffset];
            int topBound = dataBytes[headerOffset + 1];
            int frameWidth = dataBytes[headerOffset + 2];
            int frameHeight = dataBytes[headerOffset + 3];
            uint rawOffset = BitConverter.ToUInt32(dataBytes, headerOffset + 4);
            Need(rawOffset <= int.MaxValue, "Invalid offset");
            int offset = (int)rawOffset;
            Need(leftBound + frameWidth <= canvasWidth && topBound + frameHeight <= canvasHeight && offset >= 6 + 8 * frameCount && (long)offset + frameHeight * 2 <= dataBytes.Length, "Invalid frame bounds");
            byte[] pixelBytes = new byte[canvasWidth * canvasHeight];
            for (int y = 0; y < frameHeight; y++)
            {
                int dataPosition = offset + BitConverter.ToUInt16(dataBytes, offset + y * 2);
                int x = 0;
                while (x < frameWidth)
                {
                    Need(dataPosition < dataBytes.Length, "Truncated GRP");
                    int commandValue = dataBytes[dataPosition++];
                    int runLength = (commandValue & 128) != 0 ? commandValue & 127 : commandValue & 63;
                    Need(runLength > 0 && x + runLength <= frameWidth, "Invalid GRP run");
                    if ((commandValue & 128) != 0)
                    {
                        x += runLength;
                    }
                    else if ((commandValue & 64) != 0)
                    {
                        Need(dataPosition < dataBytes.Length, "Truncated repeat");
                        byte pixelValue = dataBytes[dataPosition++];
                        for (int runIndex = 0; runIndex < runLength; runIndex++)
                        {
                            pixelBytes[(topBound + y) * canvasWidth + leftBound + x++] = pixelValue;
                        }
                    }
                    else
                    {
                        Need(dataPosition + runLength <= dataBytes.Length, "Truncated literal");
                        for (int runIndex = 0; runIndex < runLength; runIndex++)
                        {
                            pixelBytes[(topBound + y) * canvasWidth + leftBound + x++] = dataBytes[dataPosition++];
                        }
                    }
                }
            }

            frameList.Add(new ImageData
            {
                Width = canvasWidth,
                Height = canvasHeight,
                Pixels = pixelBytes
            });
        }

        return frameList;
    }

    public static string Pack(string inputPath, string outputPath)
    {
        rgbFallbackCount = 0;
        inputPath = Path.GetFullPath(inputPath);
        string directoryPath = Directory.Exists(inputPath) ? inputPath : Path.GetDirectoryName(inputPath);
        string ralPath = null;
        if (Directory.Exists(inputPath) == true)
        {
            var ralPaths = Directory.GetFiles(inputPath, "*.ral");
            Need(ralPaths.Length <= 1, "Select a RAL file when there are multiple lists");
            if (ralPaths.Length == 1)
            {
                ralPath = ralPaths[0];
            }
        }
        else
        {
            Need(File.Exists(inputPath) && Path.GetExtension(inputPath).Equals(".ral", StringComparison.OrdinalIgnoreCase), "Select a folder or RAL file");
            ralPath = inputPath;
        }

        string outputName;
        string[] bmpPaths;
        if (ralPath != null)
        {
            var ralLines = File.ReadAllLines(ralPath).Select(delegate (string entryText)
            {
                return entryText.Trim();
            }).Where(delegate (string entryText)
            {
                return entryText.Length > 0;
            }).ToArray();
            Need(ralLines.Length > 1 && Path.GetExtension(ralLines[0]).Equals(".grp", StringComparison.OrdinalIgnoreCase), "RAL first line must be output.grp");
            outputName = Path.GetFileName(ralLines[0]);
            bmpPaths = ralLines.Skip(1).Select(delegate (string entryText)
            {
                Need(Path.GetExtension(entryText).Equals(".bmp", StringComparison.OrdinalIgnoreCase), "RAL accepts BMP entries only");
                return Path.GetFullPath(Path.Combine(directoryPath, entryText));
            }).ToArray();
            var listedPaths = new HashSet<string>(bmpPaths, StringComparer.OrdinalIgnoreCase);
            Need(listedPaths.Count == bmpPaths.Length, "Duplicate BMP in RAL");
            Need(Directory.GetFiles(directoryPath, "*.bmp").All(delegate (string entryText)
            {
                return listedPaths.Contains(Path.GetFullPath(entryText));
            }), "BMP files are missing from RAL; update the list before packing");
        }
        else
        {
            bmpPaths = Directory.GetFiles(directoryPath, "*.bmp");
            Need(bmpPaths.Length > 0, "No BMP files");
            var frameMatches = bmpPaths.Select(delegate (string entryText)
            {
                return Regex.Match(Path.GetFileNameWithoutExtension(entryText), @"^(.*?)(\d+)$");
            }).ToArray();
            Need(frameMatches.All(delegate (Match frameMatch)
            {
                return frameMatch.Success == true;
            }), "Without RAL use name000.bmp, name001.bmp, ...");
            string filePrefix = frameMatches[0].Groups[1].Value;
            Need(frameMatches.All(delegate (Match frameMatch)
            {
                return frameMatch.Groups[1].Value == filePrefix;
            }), "Mixed BMP prefixes require a RAL list");
            bmpPaths = bmpPaths.OrderBy(delegate (string entryText)
            {
                return int.Parse(Regex.Match(Path.GetFileNameWithoutExtension(entryText), @"\d+$").Value);
            }).ToArray();
            for (int frameIndex = 0; frameIndex < bmpPaths.Length; frameIndex++)
            {
                Need(int.Parse(Regex.Match(Path.GetFileNameWithoutExtension(bmpPaths[frameIndex]), @"\d+$").Value) == frameIndex, "Frame numbers must start at 0 without gaps");
            }

            outputName = filePrefix + ".grp";
        }

        Need(bmpPaths.Length > 0 && bmpPaths.Length <= 2400, "Frame count must be 1..2400");
        var imageList = bmpPaths.Select(ReadBmp).ToList();
        int canvasWidth = imageList[0].Width;
        int canvasHeight = imageList[0].Height;
        Need(imageList.All(delegate (ImageData imageData)
        {
            return imageData.Width == canvasWidth && imageData.Height == canvasHeight;
        }), "All frames must have the same canvas dimensions");
        var payloadList = new List<byte[]>();
        var headerList = new List<byte[]>();
        int offset = 6 + 8 * bmpPaths.Length;
        foreach (var imageData in imageList)
        {
            byte leftBound;
            byte topBound;
            byte frameWidth;
            byte frameHeight;
            byte[] frameBytes = Encode(imageData, out leftBound, out topBound, out frameWidth, out frameHeight);
            using (var memoryStream = new MemoryStream())
            {
                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(leftBound);
                    binaryWriter.Write(topBound);
                    binaryWriter.Write(frameWidth);
                    binaryWriter.Write(frameHeight);
                    binaryWriter.Write((uint)offset);
                    headerList.Add(memoryStream.ToArray());
                }
            }

            payloadList.Add(frameBytes);
            offset = checked(offset + frameBytes.Length);
        }

        byte[] grpBytes;
        using (var memoryStream = new MemoryStream())
        {
            using (var binaryWriter = new BinaryWriter(memoryStream))
            {
                binaryWriter.Write((ushort)bmpPaths.Length);
                binaryWriter.Write((ushort)canvasWidth);
                binaryWriter.Write((ushort)canvasHeight);
                foreach (var blockBytes in headerList)
                {
                    binaryWriter.Write(blockBytes);
                }

                foreach (var blockBytes in payloadList)
                {
                    binaryWriter.Write(blockBytes);
                }

                grpBytes = memoryStream.ToArray();
            }
        }

        var decodedFrames = Decode(grpBytes);
        for (int frameIndex = 0; frameIndex < imageList.Count; frameIndex++)
        {
            Need(imageList[frameIndex].Pixels.SequenceEqual(decodedFrames[frameIndex].Pixels), "Round-trip verification failed at frame " + frameIndex);
        }

        Directory.CreateDirectory(outputPath);
        string targetPath = Path.Combine(outputPath, outputName);
        string temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, grpBytes);
            if (File.Exists(targetPath) == true)
            {
                File.Replace(temporaryPath, targetPath, targetPath + ".bak");
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath) == true)
            {
                File.Delete(temporaryPath);
            }
        }

        return "RGB fallback pixels: " + rgbFallbackCount + ". Original pixel index is preferred; fallback uses lowest exact-match palette index. " + outputName + ": " + imageList.Count + " frames, " + canvasWidth + "x" + canvasHeight + ", " + grpBytes.Length + " bytes; all pixels verified -> " + targetPath;
    }

    [STAThread]
    private static int Main(string[] argumentTexts)
    {
        if (argumentTexts.Length > 0)
        {
            try
            {
                if (argumentTexts[0] == "--verify")
                {
                    var decodedFrames = Decode(File.ReadAllBytes(argumentTexts[1]));
                    Console.WriteLine(decodedFrames.Count + " frames verified");
                    return 0;
                }

                Console.WriteLine(Pack(argumentTexts[0], argumentTexts.Length > 1 ? argumentTexts[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output")));
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        Application.EnableVisualStyles();
        Application.Run(new Window());
        return 0;
    }
}

internal class Window : Form
{
    private TextBox inputTextBox = new TextBox();
    private TextBox outputTextBox = new TextBox();
    private TextBox logTextBox = new TextBox();
    public Window()
    {
        Text = "StarCraft 1.16.1 Command Icons Packer";
        ClientSize = new Size(780, 450);
        Font = new Font("Malgun Gothic", 10);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(new Label
        {
            Text = "RAL 파일 또는 BMP 폴더 (팔레트 인덱스 0 = 투명)",
            Left = 16,
            Top = 16,
            Width = 730
        });
        inputTextBox.SetBounds(16, 46, 570, 28);
        inputTextBox.Text = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "origins", "unit", "cmdbtns", "cmdicons.ral"));
        Controls.Add(inputTextBox);
        Add("RAL 선택", 596, 44, 160, delegate
        {
            using (var selectionDialog = new OpenFileDialog
            {
                Filter = "RetroGRP list|*.ral"
            })
            {
                if (selectionDialog.ShowDialog() == DialogResult.OK)
                {
                    inputTextBox.Text = selectionDialog.FileName;
                }
            }
        });
        Add("BMP 폴더 선택", 596, 80, 160, delegate
        {
            using (var selectionDialog = new FolderBrowserDialog())
            {
                if (selectionDialog.ShowDialog() == DialogResult.OK)
                {
                    inputTextBox.Text = selectionDialog.SelectedPath;
                }
            }
        });
        Controls.Add(new Label
        {
            Text = "출력 폴더",
            Left = 16,
            Top = 88,
            Width = 200
        });
        outputTextBox.SetBounds(16, 118, 570, 28);
        outputTextBox.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
        Controls.Add(outputTextBox);
        Add("출력 폴더 선택", 596, 116, 160, delegate
        {
            using (var selectionDialog = new FolderBrowserDialog())
            {
                if (selectionDialog.ShowDialog() == DialogResult.OK)
                {
                    outputTextBox.Text = selectionDialog.SelectedPath;
                }
            }
        });
        Add("GRP 만들기 + 검증", 16, 164, 240, delegate
        {
            Run(delegate
            {
                return Packer.Pack(inputTextBox.Text, outputTextBox.Text);
            });
        });
        logTextBox.SetBounds(16, 212, 740, 220);
        logTextBox.Multiline = true;
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Both;
        Controls.Add(logTextBox);
    }

    private void Add(string labelText, int x, int y, int width, Action callbackAction)
    {
        var actionButton = new Button
        {
            Text = labelText,
            Left = x,
            Top = y,
            Width = width,
            Height = 32
        };
        actionButton.Click += delegate
        {
            callbackAction();
        };
        Controls.Add(actionButton);
    }

    private void Run(Func<string> operationFunc)
    {
        try
        {
            logTextBox.AppendText(operationFunc() + Environment.NewLine);
        }
        catch (Exception exception)
        {
            logTextBox.AppendText("오류: " + exception.Message + Environment.NewLine);
            MessageBox.Show(exception.Message, "변환 실패");
        }
    }
}






