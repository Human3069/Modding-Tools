using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

internal static class TblCodec
{
    private const int MAX_FILE_SIZE = 65535;

    public static Encoding GetGameEncoding(int codePage)
    {
        return Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    public static byte[] Compile(string[] entryTexts, Encoding gameEncoding)
    {
        if (entryTexts.Length == 0 || entryTexts.Length > 65535)
        {
            throw new InvalidDataException("문자열 항목은 1~65535개여야 합니다.");
        }

        int headerSize = 2 + entryTexts.Length * 2;
        if (headerSize >= MAX_FILE_SIZE)
        {
            throw new InvalidDataException("항목 수로 인해 16비트 오프셋 범위를 초과합니다.");
        }

        List<byte[]> entryList = new List<byte[]>();
        int totalSize = headerSize;
        for (int entryIndex = 0; entryIndex < entryTexts.Length; entryIndex++)
        {
            byte[] entryBytes;
            try
            {
                entryBytes = CompileEntry(entryTexts[entryIndex], gameEncoding);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("TXT " + (entryIndex + 1) + "번째 줄: " + exception.Message, exception);
            }

            entryList.Add(entryBytes);
            totalSize = checked(totalSize + entryBytes.Length);
            if (totalSize > MAX_FILE_SIZE)
            {
                throw new InvalidDataException("TBL이 65,535바이트를 초과합니다. 현재 계산 크기: " + totalSize);
            }
        }

        using (MemoryStream memoryStream = new MemoryStream())
        {
            using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
            {
                binaryWriter.Write((ushort)entryList.Count);
                int entryOffset = headerSize;
                foreach (byte[] entryBytes in entryList)
                {
                    binaryWriter.Write((ushort)entryOffset);
                    entryOffset += entryBytes.Length;
                }

                foreach (byte[] entryBytes in entryList)
                {
                    binaryWriter.Write(entryBytes);
                }

                byte[] tblBytes = memoryStream.ToArray();
                List<byte[]> decodedEntries = ReadEntries(tblBytes);
                for (int entryIndex = 0; entryIndex < entryList.Count; entryIndex++)
                {
                    if (AreEqual(entryList[entryIndex], decodedEntries[entryIndex]) == false)
                    {
                        throw new InvalidDataException("출력 검증 실패: 항목 " + entryIndex);
                    }
                }

                return tblBytes;
            }
        }
    }

    private static byte[] CompileEntry(string entryText, Encoding gameEncoding)
    {
        List<byte> entryBytes = new List<byte>();
        int textPosition = 0;
        while (textPosition < entryText.Length)
        {
            if (entryText[textPosition] == '<')
            {
                int closePosition = entryText.IndexOf('>', textPosition + 1);
                if (closePosition < 0)
                {
                    throw new InvalidDataException("닫히지 않은 코드입니다. '<' 자체는 <60>으로 작성하세요.");
                }

                string numberText = entryText.Substring(textPosition + 1, closePosition - textPosition - 1);
                byte controlValue;
                bool isDecimal = numberText.Length > 0;
                foreach (char digitCharacter in numberText)
                {
                    if (digitCharacter < '0' || digitCharacter > '9')
                    {
                        isDecimal = false;
                    }
                }

                if (isDecimal == false || byte.TryParse(numberText, out controlValue) == false)
                {
                    throw new InvalidDataException("코드는 <0>~<255>의 십진수여야 합니다: <" + numberText + ">");
                }

                entryBytes.Add(controlValue);
                textPosition = closePosition + 1;
            }
            else
            {
                int nextPosition = entryText.IndexOf('<', textPosition);
                if (nextPosition < 0)
                {
                    nextPosition = entryText.Length;
                }

                string literalText = entryText.Substring(textPosition, nextPosition - textPosition);
                entryBytes.AddRange(gameEncoding.GetBytes(literalText));
                textPosition = nextPosition;
            }
        }

        if (entryBytes.Count == 0 || entryBytes[entryBytes.Count - 1] != 0)
        {
            entryBytes.Add(0);
        }

        return entryBytes.ToArray();
    }

    public static List<byte[]> ReadEntries(byte[] tblBytes)
    {
        if (tblBytes.Length < 4)
        {
            throw new InvalidDataException("TBL 헤더가 너무 짧습니다.");
        }

        int entryCount = BitConverter.ToUInt16(tblBytes, 0);
        int headerSize = 2 + entryCount * 2;
        if (entryCount == 0 || headerSize > tblBytes.Length)
        {
            throw new InvalidDataException("잘못된 TBL 항목 수입니다.");
        }

        int[] entryOffsets = new int[entryCount];
        SortedSet<int> boundarySet = new SortedSet<int>();
        boundarySet.Add(tblBytes.Length);
        for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
        {
            int entryOffset = BitConverter.ToUInt16(tblBytes, 2 + entryIndex * 2);
            if (entryOffset < headerSize || entryOffset >= tblBytes.Length)
            {
                throw new InvalidDataException("잘못된 TBL 오프셋입니다.");
            }

            entryOffsets[entryIndex] = entryOffset;
            boundarySet.Add(entryOffset);
        }

        Dictionary<int, int> endOffsetLookup = new Dictionary<int, int>();
        int previousOffset = -1;
        foreach (int boundaryOffset in boundarySet)
        {
            if (previousOffset >= 0)
            {
                endOffsetLookup.Add(previousOffset, boundaryOffset);
            }

            previousOffset = boundaryOffset;
        }

        List<byte[]> entryList = new List<byte[]>();
        foreach (int entryOffset in entryOffsets)
        {
            int endOffset = endOffsetLookup[entryOffset];
            byte[] entryBytes = new byte[endOffset - entryOffset];
            Buffer.BlockCopy(tblBytes, entryOffset, entryBytes, 0, entryBytes.Length);
            entryList.Add(entryBytes);
        }

        return entryList;
    }

    public static bool AreEqual(byte[] firstBytes, byte[] secondBytes)
    {
        if (firstBytes.Length != secondBytes.Length)
        {
            return false;
        }

        for (int byteIndex = 0; byteIndex < firstBytes.Length; byteIndex++)
        {
            if (firstBytes[byteIndex] != secondBytes[byteIndex])
            {
                return false;
            }
        }

        return true;
    }

    public static string CompileFile(string inputPath, string outputPath, int codePage)
    {
        inputPath = Path.GetFullPath(inputPath);
        outputPath = Path.GetFullPath(outputPath);
        if (string.Equals(inputPath, outputPath, StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidDataException("입력 TXT와 출력 TBL은 다른 경로여야 합니다.");
        }

        string[] entryTexts;
        // Strict UTF-8 avoids silently replacing damaged input with replacement characters.
        using (StreamReader textReader = new StreamReader(inputPath, new UTF8Encoding(false, true), false))
        {
            string allText = textReader.ReadToEnd();
            if (allText.Length > 0 && allText[0] == '\uFEFF')
            {
                allText = allText.Substring(1);
            }

            List<string> lineList = new List<string>();
            using (StringReader lineReader = new StringReader(allText))
            {
                string entryText;
                while ((entryText = lineReader.ReadLine()) != null)
                {
                    lineList.Add(entryText);
                }
            }

            entryTexts = lineList.ToArray();
        }

        byte[] tblBytes = Compile(entryTexts, GetGameEncoding(codePage));
        string outputDirectoryPath = Path.GetDirectoryName(outputPath);
        Directory.CreateDirectory(outputDirectoryPath);
        string temporaryPath = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, tblBytes);
            if (File.Exists(outputPath) == true)
            {
                File.Replace(temporaryPath, outputPath, outputPath + ".bak");
            }
            else
            {
                File.Move(temporaryPath, outputPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath) == true)
            {
                File.Delete(temporaryPath);
            }
        }

        return "컴파일 및 재해독 검증 완료: " + entryTexts.Length + "개 항목, " + tblBytes.Length + "바이트" + Environment.NewLine + outputPath;
    }
}

internal sealed class CompilerWindow : Form
{
    private readonly TextBox inputTextBox = new TextBox();
    private readonly TextBox outputTextBox = new TextBox();
    private readonly ComboBox encodingComboBox = new ComboBox();
    private readonly TextBox logTextBox = new TextBox();

    public CompilerWindow()
    {
        Text = "StarCraft 1.16.1 TBL Compiler";
        ClientSize = new Size(780, 400);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Malgun Gothic", 10);
        AddLabel("입력 TXT (UTF-8, 한 줄 = 한 항목)", 16, 16, 730);
        inputTextBox.SetBounds(16, 44, 590, 28);
        Controls.Add(inputTextBox);
        AddButton("TXT 선택", 620, 42, 140, SelectInput);
        AddLabel("출력 TBL", 16, 84, 730);
        outputTextBox.SetBounds(16, 112, 590, 28);
        outputTextBox.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "stat_txt.tbl");
        Controls.Add(outputTextBox);
        AddButton("저장 위치", 620, 110, 140, SelectOutput);
        AddLabel("TBL 문자 인코딩", 16, 156, 180);
        encodingComboBox.SetBounds(200, 152, 400, 28);
        encodingComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        encodingComboBox.Items.Add("영어 / 서유럽 (Windows-1252) — 기본");
        encodingComboBox.Items.Add("한국어 (CP949) — 게임 한글 표시 환경 필요");
        encodingComboBox.SelectedIndex = 0;
        Controls.Add(encodingComboBox);
        AddButton("TBL 컴파일 + 검증", 16, 198, 260, CompileSelectedFile);
        logTextBox.SetBounds(16, 246, 744, 138);
        logTextBox.Multiline = true;
        logTextBox.ReadOnly = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        Controls.Add(logTextBox);
    }

    private void AddLabel(string labelText, int leftPosition, int topPosition, int labelWidth)
    {
        Label descriptionLabel = new Label();
        descriptionLabel.Text = labelText;
        descriptionLabel.SetBounds(leftPosition, topPosition, labelWidth, 24);
        Controls.Add(descriptionLabel);
    }

    private void AddButton(string buttonText, int leftPosition, int topPosition, int buttonWidth, Action callbackAction)
    {
        Button actionButton = new Button();
        actionButton.Text = buttonText;
        actionButton.SetBounds(leftPosition, topPosition, buttonWidth, 34);
        actionButton.Click += delegate
        {
            callbackAction();
        };
        Controls.Add(actionButton);
    }

    private void SelectInput()
    {
        using (OpenFileDialog selectionDialog = new OpenFileDialog())
        {
            selectionDialog.Filter = "UTF-8 text|*.txt|All files|*.*";
            if (selectionDialog.ShowDialog() == DialogResult.OK)
            {
                inputTextBox.Text = selectionDialog.FileName;
            }
        }
    }

    private void SelectOutput()
    {
        using (SaveFileDialog selectionDialog = new SaveFileDialog())
        {
            selectionDialog.Filter = "StarCraft string table|*.tbl";
            selectionDialog.FileName = "stat_txt.tbl";
            if (selectionDialog.ShowDialog() == DialogResult.OK)
            {
                outputTextBox.Text = selectionDialog.FileName;
            }
        }
    }

    private void CompileSelectedFile()
    {
        try
        {
            int codePage = encodingComboBox.SelectedIndex == 1 ? 949 : 1252;
            logTextBox.AppendText(TblCodec.CompileFile(inputTextBox.Text, outputTextBox.Text, codePage) + Environment.NewLine);
        }
        catch (Exception exception)
        {
            logTextBox.AppendText("오류: " + exception.Message + Environment.NewLine);
            MessageBox.Show(exception.Message, "컴파일 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] argumentTexts)
    {
        if (argumentTexts.Length > 0)
        {
            try
            {
                if (argumentTexts.Length < 2 || argumentTexts.Length > 3)
                {
                    throw new ArgumentException("사용법: TblCompiler.exe input.txt output.tbl [1252|949]");
                }

                int codePage = argumentTexts.Length == 3 ? int.Parse(argumentTexts[2]) : 1252;
                if (codePage != 1252 && codePage != 949)
                {
                    throw new ArgumentException("지원 인코딩: 1252, 949");
                }

                Console.WriteLine(TblCodec.CompileFile(argumentTexts[0], argumentTexts[1], codePage));
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new CompilerWindow());
        return 0;
    }
}
