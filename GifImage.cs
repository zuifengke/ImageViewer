using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

namespace KaiwaProjects
{
    public class GifImage
    {
        private KpImageViewer KpViewer;
        private Image gif;
        private FrameDimension dimension;
        private int frameCount;
        private int rotation = 0;
        private Bitmap currentFrameBmp = null;

        public GifImage(KpImageViewer KpViewer, Image img)
        {
            this.KpViewer = KpViewer;
            this.gif = img;
            this.dimension = new FrameDimension(gif.FrameDimensionsList[0]);
            this.frameCount = gif.GetFrameCount(dimension);

            this.gif.SelectActiveFrame(dimension, 0);

            this.currentFrameBmp = (Bitmap)gif.Clone();

            UpdateAnimator();
        }

        public void UpdateAnimator()
        {
            if (KpViewer.GifAnimation)
            {
                ImageAnimator.Animate(this.gif, OnFrameChanged);
            }
            else
            {
                ImageAnimator.StopAnimate(this.gif, OnFrameChanged);
            }
        }

        public int Rotation
        {
            get { return rotation; }
        }

        public void Rotate(int rotation)
        {
            this.rotation = (this.rotation + rotation) % 360;
        }

        public void Dispose()
        {
            gif.Dispose();
        }

        private void OnFrameChanged(object o, EventArgs e)
        {
            this.currentFrameBmp = (Bitmap)gif;

            this.KpViewer.InvalidatePanel();
        }

        public Bitmap CurrentFrame
        {
            get
            {
                return currentFrameBmp;
            }
        }

        public int FrameCount
        {
            get { return frameCount; }
        }
    }
}
