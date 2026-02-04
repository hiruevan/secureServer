using System.Drawing;
using System.Windows.Forms;

namespace CustomControls
{
    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => Color.FromArgb(35, 35, 35);
        public override Color MenuStripGradientEnd => Color.FromArgb(35, 35, 35);

        public override Color ToolStripBorder => Color.FromArgb(45, 45, 45);
        public override Color ToolStripDropDownBackground => Color.FromArgb(30, 30, 30);

        public override Color ImageMarginGradientBegin => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(30, 30, 30);
        public override Color ImageMarginGradientEnd => Color.FromArgb(30, 30, 30);

        public override Color MenuItemSelected => Color.FromArgb(0, 100, 180);
        public override Color MenuItemBorder => Color.FromArgb(70, 70, 70);

        public override Color MenuItemPressedGradientBegin => Color.FromArgb(45, 45, 45);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(45, 45, 45);

        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(0, 100, 180);

        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(0, 100, 180);

    }
}
