using System.Drawing.Drawing2D;
using AIM.Core.Personalities;

namespace AIM.Desktop.WinForms;

internal static class ClassicAim
{
    public static readonly Font UiFont = new("MS Sans Serif", 9.25f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font BoldFont = new("MS Sans Serif", 9.25f, FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font SmallFont = new("MS Sans Serif", 8.25f, FontStyle.Regular, GraphicsUnit.Point);

    public static readonly Color AimBlue = Color.FromArgb(0, 0, 128);
    public static readonly Color AwayYellow = Color.FromArgb(255, 246, 196);
    public static readonly Color LinkBlue = Color.FromArgb(0, 0, 180);
    public static readonly Color TranscriptNameBlue = Color.FromArgb(0, 0, 160);
    public static readonly Color TranscriptRemoteRed = Color.FromArgb(170, 0, 0);

    public static void ApplyClassicForm(Form form)
    {
        form.Font = UiFont;
        form.BackColor = SystemColors.Control;
        form.StartPosition = FormStartPosition.CenterScreen;
    }

    public static Button Button(string text)
    {
        return new Button
        {
            Text = text,
            Font = UiFont,
            Height = 30,
            FlatStyle = FlatStyle.Standard,
            UseVisualStyleBackColor = true
        };
    }

    public static Label Label(string text, Font? font = null, Color? color = null)
    {
        return new Label
        {
            Text = text,
            Font = font ?? UiFont,
            ForeColor = color ?? SystemColors.ControlText,
            AutoSize = true
        };
    }

    public static Panel SunkenPanel()
    {
        return new Panel
        {
            BorderStyle = BorderStyle.Fixed3D,
            BackColor = SystemColors.Window
        };
    }

    public static ImageList CreateBuddyImages()
    {
        var images = new ImageList
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(20, 20)
        };

        images.Images.Add("online", DrawStatusOrb(Color.FromArgb(0, 180, 0), images.ImageSize.Width));
        images.Images.Add("away", DrawStatusOrb(Color.FromArgb(245, 170, 0), images.ImageSize.Width));
        images.Images.Add("group", DrawFolderGlyph(images.ImageSize.Width));

        return images;
    }

    public static string GetAvatarImageKey(Personality personality) => $"avatar-{personality.Id:N}";

    public static bool TryAddAvatarImage(ImageList images, Personality personality)
    {
        var path = ResolveAvatarPath(personality.AvatarImagePath);

        if (path is null || images.Images.ContainsKey(GetAvatarImageKey(personality)))
        {
            return path is not null;
        }

        using var image = Image.FromFile(path);
        using var scaled = new Bitmap(images.ImageSize.Width, images.ImageSize.Height);
        using (var graphics = Graphics.FromImage(scaled))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(image, 0, 0, images.ImageSize.Width, images.ImageSize.Height);
        }

        images.Images.Add(GetAvatarImageKey(personality), new Bitmap(scaled));
        return true;
    }

    public static PictureBox AvatarPicture(Personality personality, int size)
    {
        var picture = new PictureBox
        {
            Size = new Size(size, size),
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };
        var path = ResolveAvatarPath(personality.AvatarImagePath);

        if (path is not null)
        {
            using var image = Image.FromFile(path);
            picture.Image = new Bitmap(image);
        }
        else
        {
            picture.Image = DrawLetterAvatar(personality.AvatarText, size);
        }

        return picture;
    }

    private static string? ResolveAvatarPath(string? avatarImagePath)
    {
        if (string.IsNullOrWhiteSpace(avatarImagePath))
        {
            return null;
        }

        var path = avatarImagePath.Trim();
        var resolved = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppContext.BaseDirectory, path);

        return File.Exists(resolved) ? resolved : null;
    }

    private static Bitmap DrawLetterAvatar(string text, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(AimBlue);

        using var font = new Font("MS Sans Serif", Math.Max(8, size / 3f), FontStyle.Bold, GraphicsUnit.Point);
        using var brush = new SolidBrush(Color.White);
        var label = string.IsNullOrWhiteSpace(text) ? "?" : text.Trim()[..1].ToUpperInvariant();
        var measured = graphics.MeasureString(label, font);
        graphics.DrawString(label, font, brush, (size - measured.Width) / 2, (size - measured.Height) / 2);
        return bitmap;
    }

    private static Bitmap DrawStatusOrb(Color color, int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shadow = new SolidBrush(Color.FromArgb(80, Color.Black));
        var orb = Math.Max(10, size - 7);
        using var brush = new LinearGradientBrush(new Rectangle(4, 3, orb, orb), Color.White, color, 45);
        using var pen = new Pen(Color.FromArgb(80, 80, 80));
        graphics.FillEllipse(shadow, 5, 5, orb, orb);
        graphics.FillEllipse(brush, 4, 3, orb, orb);
        graphics.DrawEllipse(pen, 4, 3, orb, orb);
        return bitmap;
    }

    private static Bitmap DrawFolderGlyph(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.FromArgb(255, 219, 105));
        using var pen = new Pen(Color.FromArgb(120, 90, 20));
        graphics.FillRectangle(brush, 3, 7, size - 6, size - 9);
        graphics.FillRectangle(brush, 4, 4, size / 2, 4);
        graphics.DrawRectangle(pen, 3, 7, size - 6, size - 9);
        return bitmap;
    }
}
