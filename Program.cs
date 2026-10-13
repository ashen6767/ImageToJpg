using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("图片转 JPG")]
[assembly: AssemblyDescription("简洁的批量图片转 JPG 工具")]
[assembly: AssemblyCompany("OpenAI")]
[assembly: AssemblyProduct("ImageToJpg")]
[assembly: AssemblyVersion("0.1.2.0")]
[assembly: AssemblyFileVersion("0.1.2.0")]

namespace ImageToJpg
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    sealed class MainForm : Form
    {
        static readonly HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".bmp", ".gif", ".ico", ".jpeg", ".jpg", ".png", ".tif", ".tiff" };
        static readonly string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ImageToJpg", "settings.ini");

        readonly ListBox files = new ListBox();
        readonly Button addFiles = new Button();
        readonly Button addFolder = new Button();
        readonly Button remove = new Button();
        readonly Button clear = new Button();
        readonly Button output = new Button();
        readonly Button start = new Button();
        readonly Button cancel = new Button();
        readonly TextBox outputPath = new TextBox();
        readonly NumericUpDown quality = new NumericUpDown();
        readonly CheckBox recursive = new CheckBox();
        readonly CheckBox keepMetadata = new CheckBox();
        readonly ComboBox resizeMode = new ComboBox();
        readonly ComboBox background = new ComboBox();
        readonly ProgressBar progress = new ProgressBar();
        readonly Label status = new Label();
        readonly Label fileCount = new Label();
        CancellationTokenSource cancellation;

        public MainForm()
        {
            Text = "图片转 JPG " + new Version(Application.ProductVersion).ToString(3);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(720, 550);
            ClientSize = new Size(780, 590);
            Font = new Font("Microsoft YaHei UI", 9F);
            AllowDrop = true;

            var title = new Label { Text = "图片转 JPG", AutoSize = true, Font = new Font(Font.FontFamily, 18F, FontStyle.Bold), Location = new Point(20, 16) };
            var note = new Label { Text = "转换为 JPG，可选按短边等比例缩放；JPEG 格式本身不支持真正无损。", AutoSize = true, ForeColor = Color.DimGray, Location = new Point(22, 54) };

            files.Location = new Point(20, 86);
            files.Size = new Size(585, 274);
            files.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            files.HorizontalScrollbar = true;
            files.SelectionMode = SelectionMode.MultiExtended;
            files.AllowDrop = true;

            ConfigureButton(addFiles, "添加图片", 620, 86, AddFilesClicked);
            ConfigureButton(addFolder, "添加文件夹", 620, 126, AddFolderClicked);
            ConfigureButton(remove, "移除选中", 620, 181, RemoveClicked);
            ConfigureButton(clear, "清空列表", 620, 221, delegate { files.Items.Clear(); UpdateCount(); });

            fileCount.Text = "0 个文件";
            fileCount.AutoSize = true;
            fileCount.ForeColor = Color.DimGray;
            fileCount.Location = new Point(22, 367);
            fileCount.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            var outputLabel = new Label { Text = "输出到", AutoSize = true, Location = new Point(20, 399), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            outputPath.Location = new Point(78, 395);
            outputPath.Size = new Size(527, 25);
            outputPath.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            outputPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "JPG_Output");
            ConfigureButton(output, "浏览…", 620, 394, OutputClicked);

            var qualityLabel = new Label { Text = "质量", AutoSize = true, Location = new Point(20, 440), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            quality.Location = new Point(62, 436);
            quality.Size = new Size(58, 25);
            quality.Minimum = 1;
            quality.Maximum = 100;
            quality.Value = 100;
            quality.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            var resizeLabel = new Label { Text = "尺寸", AutoSize = true, Location = new Point(145, 440), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            resizeMode.Items.AddRange(new object[] { "不调整", "短边 480px", "短边 720px", "短边 1080px" });
            resizeMode.SelectedIndex = 0;
            resizeMode.DropDownStyle = ComboBoxStyle.DropDownList;
            resizeMode.Location = new Point(190, 435);
            resizeMode.Size = new Size(145, 25);
            resizeMode.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            recursive.Text = "文件夹包含子目录";
            recursive.Checked = true;
            recursive.AutoSize = true;
            recursive.Location = new Point(360, 439);
            recursive.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            keepMetadata.Text = "保留可用元数据";
            keepMetadata.Checked = true;
            keepMetadata.AutoSize = true;
            keepMetadata.Location = new Point(520, 439);
            keepMetadata.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            background.Items.AddRange(new object[] { "透明区域：白色", "透明区域：黑色" });
            background.SelectedIndex = 0;
            background.DropDownStyle = ComboBoxStyle.DropDownList;
            background.Location = new Point(20, 474);
            background.Size = new Size(175, 25);
            background.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            progress.Location = new Point(20, 521);
            progress.Size = new Size(585, 18);
            progress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            status.Text = "可拖放图片或文件夹到窗口";
            status.AutoEllipsis = true;
            status.Location = new Point(20, 549);
            status.Size = new Size(585, 25);
            status.ForeColor = Color.DimGray;
            status.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            ConfigureButton(start, "开始转换", 620, 510, StartClicked);
            start.Height = 34;
            start.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancel.Text = "取消";
            cancel.Location = new Point(620, 545);
            cancel.Size = new Size(130, 28);
            cancel.Enabled = false;
            cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancel.Click += delegate { if (cancellation != null) cancellation.Cancel(); };

            Controls.AddRange(new Control[] { title, note, files, addFiles, addFolder, remove, clear, fileCount, outputLabel, outputPath, output, qualityLabel, quality, resizeLabel, resizeMode, recursive, keepMetadata, background, progress, status, start, cancel });
            LoadSettings();
            FormClosing += delegate { SaveSettings(); };
            DragEnter += DragEnterHandler;
            DragDrop += DragDropHandler;
            files.DragEnter += DragEnterHandler;
            files.DragDrop += DragDropHandler;
        }

        void ConfigureButton(Button button, string text, int x, int y, EventHandler handler)
        {
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(130, 32);
            button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button.Click += handler;
        }

        void AddFilesClicked(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "支持的图片|*.bmp;*.gif;*.ico;*.jpeg;*.jpg;*.png;*.tif;*.tiff|所有文件|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(dialog.FileNames);
            }
        }

        void AddFolderClicked(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
                if (dialog.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { dialog.SelectedPath });
        }

        void OutputClicked(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (Directory.Exists(outputPath.Text)) dialog.SelectedPath = outputPath.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) outputPath.Text = dialog.SelectedPath;
            }
        }

        void RemoveClicked(object sender, EventArgs e)
        {
            var selected = files.SelectedItems.Cast<object>().ToArray();
            foreach (var item in selected) files.Items.Remove(item);
            UpdateCount();
        }

        void DragEnterHandler(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        void DragDropHandler(object sender, DragEventArgs e)
        {
            var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (paths != null) AddPaths(paths);
        }

        void AddPaths(IEnumerable<string> paths)
        {
            var current = new HashSet<string>(files.Items.Cast<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
            {
                if (File.Exists(path) && Extensions.Contains(Path.GetExtension(path))) current.Add(Path.GetFullPath(path));
                else if (Directory.Exists(path))
                {
                    var option = recursive.Checked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(path, "*", option))
                            if (Extensions.Contains(Path.GetExtension(file))) current.Add(Path.GetFullPath(file));
                    }
                    catch (Exception ex) { status.Text = "部分文件无法读取：" + ex.Message; }
                }
            }
            files.BeginUpdate();
            files.Items.Clear();
            files.Items.AddRange(current.OrderBy(x => x).Cast<object>().ToArray());
            files.EndUpdate();
            UpdateCount();
        }

        void UpdateCount() { fileCount.Text = files.Items.Count + " 个文件"; }

        void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    int separator = line.IndexOf('=');
                    if (separator > 0) values[line.Substring(0, separator)] = line.Substring(separator + 1);
                }

                string value;
                int number;
                bool flag;
                if (values.TryGetValue("OutputPath", out value) && !String.IsNullOrWhiteSpace(value)) outputPath.Text = value;
                if (values.TryGetValue("Quality", out value) && Int32.TryParse(value, out number) && number >= quality.Minimum && number <= quality.Maximum) quality.Value = number;
                if (values.TryGetValue("ResizeMode", out value) && Int32.TryParse(value, out number) && number >= 0 && number < resizeMode.Items.Count) resizeMode.SelectedIndex = number;
                if (values.TryGetValue("Recursive", out value) && Boolean.TryParse(value, out flag)) recursive.Checked = flag;
                if (values.TryGetValue("KeepMetadata", out value) && Boolean.TryParse(value, out flag)) keepMetadata.Checked = flag;
                if (values.TryGetValue("Background", out value) && Int32.TryParse(value, out number) && number >= 0 && number < background.Items.Count) background.SelectedIndex = number;
            }
            catch { }
        }

        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllLines(SettingsPath, new[]
                {
                    "OutputPath=" + outputPath.Text.Trim(),
                    "Quality=" + quality.Value,
                    "ResizeMode=" + resizeMode.SelectedIndex,
                    "Recursive=" + recursive.Checked,
                    "KeepMetadata=" + keepMetadata.Checked,
                    "Background=" + background.SelectedIndex
                });
            }
            catch { }
        }

        async void StartClicked(object sender, EventArgs e)
        {
            if (files.Items.Count == 0) { MessageBox.Show(this, "请先添加图片。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (String.IsNullOrWhiteSpace(outputPath.Text)) { MessageBox.Show(this, "请选择输出文件夹。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

            string destination;
            try { destination = Path.GetFullPath(outputPath.Text.Trim()); Directory.CreateDirectory(destination); }
            catch (Exception ex) { MessageBox.Show(this, "无法创建输出文件夹：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            var input = files.Items.Cast<string>().ToArray();
            int jpegQuality = (int)quality.Value;
            int shortEdge = resizeMode.SelectedIndex == 1 ? 480 : resizeMode.SelectedIndex == 2 ? 720 : resizeMode.SelectedIndex == 3 ? 1080 : 0;
            bool metadata = keepMetadata.Checked;
            Color matte = background.SelectedIndex == 1 ? Color.Black : Color.White;
            cancellation = new CancellationTokenSource();
            SetBusy(true);
            progress.Maximum = input.Length;
            progress.Value = 0;
            int converted = 0, failed = 0;

            await System.Threading.Tasks.Task.Run(delegate
            {
                for (int i = 0; i < input.Length; i++)
                {
                    if (cancellation.IsCancellationRequested) break;
                    string source = input[i];
                    try
                    {
                        string target = UniquePath(destination, Path.GetFileNameWithoutExtension(source) + ".jpg");
                        ConvertImage(source, target, jpegQuality, matte, metadata, shortEdge);
                        converted++;
                    }
                    catch { failed++; }
                    int done = i + 1;
                    BeginInvoke((Action)delegate { progress.Value = done; status.Text = "正在处理：" + Path.GetFileName(source); });
                }
            });

            bool wasCancelled = cancellation.IsCancellationRequested;
            cancellation.Dispose();
            cancellation = null;
            SetBusy(false);
            status.Text = wasCancelled ? "已取消" : "转换完成";
            MessageBox.Show(this, "成功：" + converted + "\n失败：" + failed + (wasCancelled ? "\n任务已取消。" : ""), "处理结果", MessageBoxButtons.OK, failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        void SetBusy(bool busy)
        {
            addFiles.Enabled = addFolder.Enabled = remove.Enabled = clear.Enabled = output.Enabled = start.Enabled = !busy;
            outputPath.Enabled = quality.Enabled = resizeMode.Enabled = recursive.Enabled = keepMetadata.Enabled = background.Enabled = !busy;
            cancel.Enabled = busy;
        }

        static string UniquePath(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path)) return path;
            string stem = Path.GetFileNameWithoutExtension(fileName);
            for (int i = 2; ; i++)
            {
                path = Path.Combine(directory, stem + " (" + i + ").jpg");
                if (!File.Exists(path)) return path;
            }
        }

        static void ConvertImage(string source, string target, int quality, Color matte, bool keepMetadata, int shortEdge)
        {
            using (var image = Image.FromFile(source))
            {
                ApplyOrientation(image);
                Size targetSize = CalculateSize(image.Width, image.Height, shortEdge);
                using (var bitmap = new Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format24bppRgb))
                {
                    bitmap.SetResolution(SafeDpi(image.HorizontalResolution), SafeDpi(image.VerticalResolution));
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(matte);
                        graphics.CompositingQuality = CompositingQuality.HighQuality;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        if (targetSize.Width == image.Width && targetSize.Height == image.Height)
                            graphics.DrawImageUnscaled(image, 0, 0);
                        else
                            graphics.DrawImage(image, new Rectangle(0, 0, targetSize.Width, targetSize.Height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel);
                    }
                    if (keepMetadata)
                    {
                        foreach (var item in image.PropertyItems)
                        {
                            if (item.Id == 0x0112) continue;
                            try { bitmap.SetPropertyItem(item); } catch { }
                        }
                    }
                    var codec = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
                    using (var parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                        bitmap.Save(target, codec, parameters);
                    }
                }
            }
        }

        static Size CalculateSize(int width, int height, int shortEdge)
        {
            int currentShortEdge = Math.Min(width, height);
            if (shortEdge <= 0 || currentShortEdge <= shortEdge) return new Size(width, height);
            double scale = (double)shortEdge / currentShortEdge;
            return new Size(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
        }

        static float SafeDpi(float value) { return value > 0 && value < 10000 ? value : 96F; }

        static void ApplyOrientation(Image image)
        {
            const int id = 0x0112;
            if (!image.PropertyIdList.Contains(id)) return;
            int orientation = image.GetPropertyItem(id).Value[0];
            RotateFlipType transform = RotateFlipType.RotateNoneFlipNone;
            switch (orientation)
            {
                case 2: transform = RotateFlipType.RotateNoneFlipX; break;
                case 3: transform = RotateFlipType.Rotate180FlipNone; break;
                case 4: transform = RotateFlipType.Rotate180FlipX; break;
                case 5: transform = RotateFlipType.Rotate90FlipX; break;
                case 6: transform = RotateFlipType.Rotate90FlipNone; break;
                case 7: transform = RotateFlipType.Rotate270FlipX; break;
                case 8: transform = RotateFlipType.Rotate270FlipNone; break;
            }
            if (transform != RotateFlipType.RotateNoneFlipNone) image.RotateFlip(transform);
        }
    }
}
