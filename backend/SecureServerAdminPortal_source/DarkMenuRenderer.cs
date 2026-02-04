using System;
using System.Drawing;
using System.Windows.Forms;

namespace CustomControls
{
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.White;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(0, 100, 180)))
                {
                    e.Graphics.FillRectangle(
                        brush,
                        e.Item.ContentRectangle
                    );
                }
            }
            else
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(35, 35, 35)))
                {
                    e.Graphics.FillRectangle(
                        brush,
                        e.Item.ContentRectangle
                    );
                }
            }
        }

        // 🔥 THIS is what fixes the black arrow
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Color.White;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen pen = new Pen(Color.FromArgb(60, 60, 60)))
            {
                int y = e.Item.ContentRectangle.Height / 2;
                e.Graphics.DrawLine(
                    pen,
                    30,
                    y,
                    e.Item.ContentRectangle.Right,
                    y
                );
            }
        }
    }
}
