using ShareX.HelpersLib;
using SkiaSharp;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ShareX
{
    public partial class TaskSettingsForm
    {
        private static readonly GIFQuality[] GIFQualityOptions =
        {
            GIFQuality.Adaptive, GIFQuality.Bit8, GIFQuality.Bit4, GIFQuality.Grayscale
        };

        private void InitializeGIFQualityOptions()
        {
            var resources = new ComponentResourceManager(typeof(TaskSettingsForm));
            cbImageGIFQuality.Items.AddRange(new[]
            {
                resources.GetString("ImageEncoder.GIFAdaptive"),
                resources.GetString("ImageEncoder.GIFOctree256"),
                resources.GetString("ImageEncoder.GIFOctree16"),
                resources.GetString("ImageEncoder.GIFGrayscale")
            });

            int index = Array.IndexOf(GIFQualityOptions, TaskSettings.ImageSettings.ImageGIFQuality);
            cbImageGIFQuality.SelectedIndex = index >= 0 ? index : 1;
        }

        private static readonly SKPngEncoderFilterFlags[] PNGFilters =
        {
            SKPngEncoderFilterFlags.AllFilters, SKPngEncoderFilterFlags.None, SKPngEncoderFilterFlags.Sub,
            SKPngEncoderFilterFlags.Up, SKPngEncoderFilterFlags.Avg, SKPngEncoderFilterFlags.Paeth
        };

        private static readonly SKJpegEncoderDownsample[] JPEGSubsamplings =
        {
            SKJpegEncoderDownsample.Downsample420, SKJpegEncoderDownsample.Downsample422,
            SKJpegEncoderDownsample.Downsample444
        };

        private Label lblImagePNGCompression, lblImagePNGFilter, lblImageJPEGSubsampling;
        private Label lblImageBMPBitDepth, lblImageWebPQuality, lblImageWebPEffort;
        private NumericUpDown nudImagePNGCompression, nudImageWebPQuality, nudImageWebPEffort;
        private ComboBox cbImagePNGFilter, cbImageJPEGSubsampling, cbImageBMPBitDepth;
        private CheckBox cbImageWebPLossless;
        private TrackBar tbImagePNGCompression, tbImageJPEGQuality, tbImageWebPQuality, tbImageWebPEffort;

        private void InitializeImageEncoderSettings()
        {
            var resources = new ComponentResourceManager(typeof(TaskSettingsForm));
            TaskSettingsImage image = TaskSettings.ImageSettings;
            pImage.SuspendLayout();

            if (IsDefault)
            {
                pImage.Top = 0;
                pImage.Height = tpQuality.ClientSize.Height;
            }

            cbImageFormat.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            cbImagePNGBitDepth.Anchor = cbImageGIFQuality.Anchor = cbImageFileExist.Anchor = cbImageFormat.Anchor;
            lblImageJPEGQualityHint.Visible = false;

            lblImagePNGCompression = AddImageEncoderLabel(resources.GetString("ImageEncoder.PNGCompression"));
            nudImagePNGCompression = AddImageEncoderNumber(9, image.ImagePNGCompressionLevel);
            tbImagePNGCompression = AddImageEncoderSlider(nudImagePNGCompression);
            nudImagePNGCompression.ValueChanged += (s, e) => image.ImagePNGCompressionLevel = (int)nudImagePNGCompression.Value;

            lblImagePNGFilter = AddImageEncoderLabel(resources.GetString("ImageEncoder.PNGFilter"));
            cbImagePNGFilter = AddImageEncoderCombo(new[]
            {
                resources.GetString("ImageEncoder.FilterAutomatic"), resources.GetString("ImageEncoder.FilterNone"),
                resources.GetString("ImageEncoder.FilterSub"), resources.GetString("ImageEncoder.FilterUp"),
                resources.GetString("ImageEncoder.FilterAverage"), resources.GetString("ImageEncoder.FilterPaeth")
            });
            cbImagePNGFilter.SelectedIndex = Math.Max(0, Array.IndexOf(PNGFilters, image.ImagePNGFilter));
            cbImagePNGFilter.SelectedIndexChanged += (s, e) => image.ImagePNGFilter = PNGFilters[cbImagePNGFilter.SelectedIndex];

            tbImageJPEGQuality = AddImageEncoderSlider(nudImageJPEGQuality);

            lblImageJPEGSubsampling = AddImageEncoderLabel(resources.GetString("ImageEncoder.JPEGSubsampling"));
            cbImageJPEGSubsampling = AddImageEncoderCombo(new[]
            {
                resources.GetString("ImageEncoder.JPEG420"), resources.GetString("ImageEncoder.JPEG422"),
                resources.GetString("ImageEncoder.JPEG444")
            });
            cbImageJPEGSubsampling.SelectedIndex = Math.Max(0, Array.IndexOf(JPEGSubsamplings, image.ImageJPEGSubsampling));
            cbImageJPEGSubsampling.SelectedIndexChanged += (s, e) => image.ImageJPEGSubsampling = JPEGSubsamplings[cbImageJPEGSubsampling.SelectedIndex];

            lblImageBMPBitDepth = AddImageEncoderLabel(resources.GetString("ImageEncoder.BMPBitDepth"));
            cbImageBMPBitDepth = AddImageEncoderCombo(Helpers.GetLocalizedEnumDescriptions<BMPBitDepth>());
            cbImageBMPBitDepth.SelectedIndex = (int)image.ImageBMPBitDepth;
            cbImageBMPBitDepth.SelectedIndexChanged += (s, e) => image.ImageBMPBitDepth = (BMPBitDepth)cbImageBMPBitDepth.SelectedIndex;

            cbImageWebPLossless = new CheckBox
            {
                Text = resources.GetString("ImageEncoder.WebPLossless"),
                Checked = image.ImageWebPLossless,
                AutoSize = true,
                UseVisualStyleBackColor = true
            };
            pImage.Controls.Add(cbImageWebPLossless);
            cbImageWebPLossless.CheckedChanged += (s, e) =>
            {
                image.ImageWebPLossless = cbImageWebPLossless.Checked;
                UpdateImageEncoderOptions();
            };

            lblImageWebPQuality = AddImageEncoderLabel(resources.GetString("ImageEncoder.WebPQuality"));
            nudImageWebPQuality = AddImageEncoderNumber(100, image.ImageWebPQuality);
            tbImageWebPQuality = AddImageEncoderSlider(nudImageWebPQuality);
            nudImageWebPQuality.ValueChanged += (s, e) => image.ImageWebPQuality = (int)nudImageWebPQuality.Value;

            lblImageWebPEffort = AddImageEncoderLabel(resources.GetString("ImageEncoder.WebPEffort"));
            nudImageWebPEffort = AddImageEncoderNumber(100, image.ImageWebPCompressionEffort);
            tbImageWebPEffort = AddImageEncoderSlider(nudImageWebPEffort);
            nudImageWebPEffort.ValueChanged += (s, e) => image.ImageWebPCompressionEffort = (int)nudImageWebPEffort.Value;

            pImage.Resize += (s, e) => UpdateImageEncoderOptions();
            pImage.ResumeLayout(false);
            UpdateImageEncoderOptions();
        }

        private Label AddImageEncoderLabel(string text)
        {
            var label = new Label { Text = text, AutoSize = false, Height = 17, TextAlign = ContentAlignment.MiddleLeft };
            pImage.Controls.Add(label);
            return label;
        }

        private NumericUpDown AddImageEncoderNumber(int max, int value)
        {
            var number = new NumericUpDown
            {
                Minimum = 0,
                Maximum = max,
                Value = Math.Clamp(value, 0, max),
                Width = 70
            };
            pImage.Controls.Add(number);
            return number;
        }

        private ComboBox AddImageEncoderCombo(string[] items)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(items);
            pImage.Controls.Add(combo);
            return combo;
        }

        private TrackBar AddImageEncoderSlider(NumericUpDown number)
        {
            var slider = new TrackBar
            {
                Minimum = (int)number.Minimum,
                Maximum = (int)number.Maximum,
                Value = (int)number.Value,
                TickStyle = TickStyle.None,
                AutoSize = false,
                Height = 23
            };
            pImage.Controls.Add(slider);
            slider.ValueChanged += (s, e) => number.Value = slider.Value;
            number.ValueChanged += (s, e) => slider.Value = (int)number.Value;
            return slider;
        }

        private void SetImageEncoderRow(Label label, Control control, bool visible, ref int top, int width)
        {
            label.Visible = control.Visible = visible;
            if (!visible) return;

            label.SetBounds(8, top, width, 17);
            control.SetBounds(8, top + 18, width, 22);
            top += 49;
        }

        private void SetImageEncoderSliderRow(Label label, TrackBar slider, NumericUpDown number, bool visible, ref int top, int width)
        {
            label.Visible = slider.Visible = number.Visible = visible;
            if (!visible) return;

            label.SetBounds(8, top, width, 17);
            slider.SetBounds(8, top + 17, width - 78, 24);
            number.SetBounds(width - 62, top + 18, 70, 22);
            top += 49;
        }

        private void UpdateImageEncoderOptions()
        {
            if (cbImagePNGFilter == null) return;

            EImageFormat format = TaskSettings.ImageSettings.ImageFormat;
            bool png = format == EImageFormat.PNG;
            bool jpeg = format == EImageFormat.JPEG;
            bool gif = format == EImageFormat.GIF;
            bool bmp = format == EImageFormat.BMP;
            bool webp = format == EImageFormat.WEBP;
            int width = Math.Max(100, pImage.ClientSize.Width - 16);
            int top = 60;

            pImage.SuspendLayout();
            lblImageFormat.Location = new Point(8, 5);
            cbImageFormat.SetBounds(8, 23, width, 23);
            SetImageEncoderRow(lblImagePNGBitDepth, cbImagePNGBitDepth, png, ref top, width);
            SetImageEncoderSliderRow(lblImagePNGCompression, tbImagePNGCompression, nudImagePNGCompression, png, ref top, width);
            SetImageEncoderRow(lblImagePNGFilter, cbImagePNGFilter, png, ref top, width);
            SetImageEncoderSliderRow(lblImageJPEGQuality, tbImageJPEGQuality, nudImageJPEGQuality, jpeg, ref top, width);
            SetImageEncoderRow(lblImageJPEGSubsampling, cbImageJPEGSubsampling, jpeg, ref top, width);
            SetImageEncoderRow(lblImageGIFQuality, cbImageGIFQuality, gif, ref top, width);
            SetImageEncoderRow(lblImageBMPBitDepth, cbImageBMPBitDepth, bmp, ref top, width);

            cbImageWebPLossless.Visible = webp;
            if (webp)
            {
                cbImageWebPLossless.Location = new Point(8, top);
                top += 28;
            }
            SetImageEncoderSliderRow(lblImageWebPQuality, tbImageWebPQuality, nudImageWebPQuality,
                webp && !cbImageWebPLossless.Checked, ref top, width);
            SetImageEncoderSliderRow(lblImageWebPEffort, tbImageWebPEffort, nudImageWebPEffort, webp, ref top, width);

            top += 10;
            cbImageAutoUseJPEG.AutoSize = false;
            cbImageAutoUseJPEG.SetBounds(8, top, width, 37);
            top += 38;
            nudImageAutoUseJPEGSize.SetBounds(8, top, 100, 23);
            lblImageSizeLimitHint.Location = new Point(113, top + 4);
            top += 28;
            cbImageAutoJPEGQuality.AutoSize = false;
            cbImageAutoJPEGQuality.SetBounds(8, top, width, 37);
            top += 44;
            lblImageFileExist.Location = new Point(8, top);
            cbImageFileExist.SetBounds(8, top + 18, width, 23);
            pImage.ResumeLayout(false);
        }
    }
}
