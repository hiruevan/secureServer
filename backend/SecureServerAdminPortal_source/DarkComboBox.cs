using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomControls
{
    public class DarkComboBox : ComboBox
    {
        public Color ButtonBackColor { get; set; } = Color.FromArgb(35, 35, 35);
        public Color ArrowColor { get; set; } = Color.White;
        public Color TextBackColor { get; set; } = Color.FromArgb(50, 50, 50);
        public Color TextForeColor { get; set; } = Color.White;

        public DarkComboBox()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(TextBackColor);

            // Draw border
            using (Pen pen = new Pen(Color.FromArgb(50, 50, 50)))
                g.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);

            // Draw text
            TextRenderer.DrawText(g, this.Text, this.Font, new Rectangle(2, 2, this.Width - 20, this.Height - 4), TextForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

            // Draw button
            int buttonWidth = SystemInformation.HorizontalScrollBarArrowWidth;
            Rectangle buttonRect = new Rectangle(this.Width - buttonWidth, 0, buttonWidth, this.Height);
            using (SolidBrush brush = new SolidBrush(ButtonBackColor))
                g.FillRectangle(brush, buttonRect);

            // Draw arrow
            int midY = this.Height / 2;
            Point[] arrow = new Point[]
            {
                new Point(buttonRect.Left + 4, midY - 3),
                new Point(buttonRect.Left + buttonWidth - 4, midY - 3),
                new Point(buttonRect.Left + buttonWidth / 2, midY + 2)
            };
            g.FillPolygon(new SolidBrush(ArrowColor), arrow);

            base.OnPaint(e);
        }
    }
}
