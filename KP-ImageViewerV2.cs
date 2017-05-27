using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.ComponentModel;

namespace KaiwaProjects
{
    public enum KpZoom
    {
        ZoomIn,
        ZoomOut
    }

    public partial class KpImageViewer : UserControl
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern short GetKeyState(int key);

        private KP_DrawEngine drawEngine;
        private KP_DrawObject drawing;
        private Bitmap preview;

        private bool animationEnabled = false;
        private bool selectMode = false;
        private bool shiftSelecting = false;
        private Point ptSelectionStart = new Point();
        private Point ptSelectionEnd = new Point();

        private bool panelDragging = false;
        private bool showPreview = true;
        private Cursor grabCursor = null;
        private Cursor dragCursor = null;

        public delegate void ImageViewerRotationEventHandler(object sender, ImageViewerRotationEventArgs e);
        public event ImageViewerRotationEventHandler AfterRotation;

        protected virtual void OnRotation(ImageViewerRotationEventArgs e)
        {
            if (AfterRotation != null)
            {
                AfterRotation(this, e);
            }
        }

        public delegate void ImageViewerZoomEventHandler(object sender, ImageViewerZoomEventArgs e);
        public event ImageViewerZoomEventHandler AfterZoom;

        protected virtual void OnZoom(ImageViewerZoomEventArgs e)
        {
            if (AfterZoom != null)
            {
                AfterZoom(this, e);
            }
        }

        public void InvalidatePanel()
        {
            this.pbFull.Invalidate();
        }

        public bool GifAnimation
        {
            get { return animationEnabled; }
            set
            {
                animationEnabled = value;

                if (this.drawing != null)
                {
                    if (this.drawing.Gif != null)
                    {
                        this.drawing.Gif.UpdateAnimator();
                    }
                }
            }
        }

        private bool IsKeyPressed(int key)
        {
            bool keyPressed = false;
            short result = GetKeyState(key);

            switch (result)
            {
                case 0:
                    // Not pressed and not toggled
                    keyPressed = false;
                    break;

                case 1:
                    // Not presses but toggled
                    keyPressed = false;
                    break;

                default:
                    // Pressed
                    keyPressed = true;
                    break;
            }

            return keyPressed;
        }

        public bool OpenButton
        {
            get { return btnOpen.Visible; }
            set
            {
                if (value)
                {
                    btnOpen.Show();

                    if (btnOpen.Visible == true)
                    {
                        // Making sure it's aligned properly
                        btnPreview.Location = new Point(198, btnPreview.Location.Y);
                    }
                    else
                    {
                        // Making sure it's aligned properly
                        btnPreview.Location = new Point(btnOpen.Location.X, btnPreview.Location.Y);
                    }
                }
                else
                {
                    btnOpen.Hide();

                    if (btnOpen.Visible == true)
                    {
                        // Making sure it's aligned properly
                        btnPreview.Location = new Point(198, btnPreview.Location.Y);
                    }
                    else
                    {
                        // Making sure it's aligned properly
                        btnPreview.Location = new Point(btnOpen.Location.X, btnPreview.Location.Y);
                    }
                }
            }
        }


        public bool PreviewButton
        {
            get { return btnPreview.Visible; }
            set
            {
                if (value)
                {
                    if (btnOpen.Visible == true)
                    {
                        // Making sure it's aligned properly
                        btnPreview.Location = new Point(198, btnPreview.Location.Y);
                    }
                    else
                    {
                        // Making sure it's aligned properly
                        btnPreview.Location = new Point(btnOpen.Location.X, btnPreview.Location.Y);
                    }

                    btnPreview.Show();
                }
                else
                {
                    btnPreview.Hide();
                }
            }
        }

        public override bool AllowDrop
        {
            get
            {
                return base.AllowDrop;
            }
            set
            {
                this.pbFull.AllowDrop = value;
                base.AllowDrop = value;
            }
        }

        public double Zoom
        {
            get { return Math.Round(drawing.Zoom * 100, 0); }
        }

        public Size OriginalSize
        {
            get { return drawing.OriginalSize; }
        }

        public Size CurrentSize
        {
            get { return drawing.CurrentSize; }
        }

        public Color MenuColor
        {
            get { return panelMenu.BackColor; }
            set
            {
                panelMenu.BackColor = value;
                panelPreview.BackColor = value;
                panelNavigation.BackColor = value;
            }
        }

        public Color MenuPanelColor
        {
            get { return panelMenu.BackColor; }
            set
            {
                panelMenu.BackColor = value;
            }
        }

        public Color NavigationPanelColor
        {
            get { return panelNavigation.BackColor; }
            set
            {
                panelNavigation.BackColor = value;
            }
        }

        public Color PreviewPanelColor
        {
            get { return panelPreview.BackColor; }
            set
            {
                panelPreview.BackColor = value;
            }
        }

        public Color NavigationTextColor
        {
            get { return lblNavigation.ForeColor; }
            set { lblNavigation.ForeColor = value; }
        }

        public Color TextColor
        {
            get { return lblPreview.ForeColor; }
            set
            {
                lblPreview.ForeColor = value;
                lblNavigation.ForeColor = value;
            }
        }

        public Color PreviewTextColor
        {
            get { return lblPreview.ForeColor; }
            set { lblPreview.ForeColor = value; }
        }

        public Color BackgroundColor
        {
            get { return pbFull.BackColor; }
            set { pbFull.BackColor = value; }
        }

        public string PreviewText
        {
            get { return lblPreview.Text; }
            set { lblPreview.Text = value; }
        }

        public string ImagePath
        {
            set 
            { 
                drawing.ImagePath = value;

                UpdatePanels(true);
                ToggleMultiPage();
            }
        }

        public Bitmap Image
        {
            get
            {
                return drawing.Image;
            }
            set
            {
                drawing.Image = value;

                UpdatePanels(true);
                ToggleMultiPage();
            }
        }

        public int Rotation
        {
            get { return drawing.Rotation; }
            set
            {
                // Making sure the rotation is 0, 90, 180 or 270 degrees!
                if (value == 90 || value == 180 || value == 270 || value == 0)
                {
                     drawing.Rotation = value;
                }
            }
        }

        private void Preview()
        {
            // Hide preview panel mechanics
            // Making sure that UpdatePanels doesn't get called when it's hidden!

            if (showPreview != pbPanel.Visible)
            {
                if (showPreview == false)
                {
                    panelPreview.Hide();
                    pbPanel.Hide();

                    pbFull.Width = pbFull.Width + (4 + panelPreview.Width);

                    if (drawing.MultiPage)
                    {
                        panelNavigation.Location = panelPreview.Location;
                    }
                    else
                    {
                        panelMenu.Width = pbFull.Width;
                    }

                    InitControl();
                    drawing.AvoidOutOfScreen();
                    pbFull.Refresh();
                }
                else
                {
                    panelPreview.Show();
                    pbPanel.Show();

                    pbFull.Width = pbFull.Width - (4 + panelPreview.Width);

                    if (drawing.MultiPage)
                    {
                        panelNavigation.Location = new Point(panelPreview.Location.X, (pbPanel.Location.Y + (pbPanel.Size.Height + 5)));
                    }
                    else
                    {
                        panelMenu.Width = pbFull.Width;
                    }
                                        
                    InitControl();
                    drawing.AvoidOutOfScreen();
                    pbFull.Refresh();

                    UpdatePanels(true);
                }
            }
        }

        public bool ShowPreview
        {
            get { return showPreview; }
            set
            {
                if (showPreview != value)
                {
                    showPreview = value;
                    Preview();
                }
            }
        }

        public KpImageViewer()
        {
            // DrawEngine & DrawObject initiralization
            drawEngine = new KP_DrawEngine();
            drawing = new KP_DrawObject(this);

            // Stream to initialize the cursors.
            Stream imgStream = null;

            try
            {
                Assembly a = Assembly.GetExecutingAssembly();

                imgStream = a.GetManifestResourceStream("KaiwaProjects.Resources.Grab.cur");
                if (imgStream != null)
                {
                    grabCursor = new Cursor(imgStream);
                    imgStream = null;
                }

                imgStream = a.GetManifestResourceStream("KaiwaProjects.Resources.Drag.cur");
                if (imgStream != null)
                {
                    dragCursor = new Cursor(imgStream);
                    imgStream = null;
                }
            }
            catch
            {
                // Cursors could not be found
            }

            InitializeComponent();

            InitControl();

            Preview();
        }

        private void DisposeControl()
        {
            // No memory leaks here
            if (drawing != null)
            {
                drawing.Dispose();
            }

            if (drawEngine != null)
            {
                drawEngine.Dispose();
            }

            if (preview != null)
            {
                preview.Dispose();
            }
        }

        public void InitControl()
        {
            // Make sure panel is DoubleBuffering
            drawEngine.CreateDoubleBuffer(pbFull.CreateGraphics(), pbFull.Size.Width, pbFull.Size.Height, pbPanel.Size.Width, pbPanel.Size.Height);
        }

        private void FocusOnMe()
        {
            // Do not lose focus! ("Fix" for the Scrolling issue)
            this.Focus();
        }

        private void KP_ImageViewerV2_Load(object sender, EventArgs e)
        {
            // Loop for ComboBox Items! Increments by 25%
            for (double z = 0.25; z <= 4.0; z = z + 0.25)
            {
                cbZoom.Items.Add(z * 100 + "%");
            }

            cbZoom.SelectedIndex = 3;
        }

        private void ToggleMultiPage()
        {
            if (drawing.MultiPage)
            {
                if (!showPreview)
                {
                    panelNavigation.Location = panelPreview.Location;

                    panelMenu.Width = panelPreview.Right - 2 - (4 + panelPreview.Width);
                    pbFull.Width = panelPreview.Right - 2;
                }
                else
                {
                    panelNavigation.Location = new Point(panelPreview.Location.X, (pbPanel.Location.Y + (pbPanel.Size.Height + 5)));

                    panelMenu.Width = panelPreview.Right - 2 - (4 + panelPreview.Width);
                    pbFull.Width = panelPreview.Right - 2 - (4 + panelPreview.Width);
                }

                panelNavigation.Show();
                lblNavigation.Text = "/ " + drawing.Pages.ToString();
                tbNavigation.Text = (drawing.CurrentPage + 1).ToString();
            }
            else
            {
                if (!showPreview)
                {
                    panelMenu.Width = panelPreview.Right - 2;
                }
                else
                {
                    panelMenu.Width = pbFull.Width;
                }

                panelNavigation.Hide();
                lblNavigation.Text = "/ 0";
                tbNavigation.Text = "0";
            }
        }

        private void KP_ImageViewerV2_Resize(object sender, EventArgs e)
        {
            InitControl();
            drawing.AvoidOutOfScreen();
            UpdatePanels(true);
        }

        private void pbFull_Paint(object sender, PaintEventArgs e)
        {
            // Can I double buffer?
            if (drawEngine.CanDoubleBuffer())
            {
                // Yes I can!
                drawEngine.g.FillRectangle(new SolidBrush(pbFull.BackColor), e.ClipRectangle.X, e.ClipRectangle.Y, e.ClipRectangle.Width, e.ClipRectangle.Height);

                // Drawing to backBuffer
                drawing.Draw(drawEngine.g);

                if (animationEnabled)
                {
                    ImageAnimator.UpdateFrames();
                }

                // Drawing to Panel
                drawEngine.Render(e.Graphics);
            }
        }

        private void pbFull_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Left Shift or Right Shift pressed? Or is select mode one?
                if (this.IsKeyPressed(0xA0) || this.IsKeyPressed(0xA1) || selectMode == true)
                {
                    // Fancy cursor
                    pbFull.Cursor = Cursors.Cross;

                    shiftSelecting = true;

                    // Initial seleciton
                    ptSelectionStart.X = e.X;
                    ptSelectionStart.Y = e.Y;

                    // No selection end
                    ptSelectionEnd.X = -1;
                    ptSelectionEnd.Y = -1;
                }
                else
                {
                    // Start dragging
                    drawing.BeginDrag(new Point(e.X, e.Y));

                    // Fancy cursor
                    if (grabCursor != null)
                    {
                        pbFull.Cursor = grabCursor;
                    }
                }
            }
        }

        private void pbFull_MouseUp(object sender, MouseEventArgs e)
        {
            // Am i dragging or selecting?
            if (shiftSelecting == true)
            {
                // Calculate my selection rectangle
                Rectangle rect = CalculateReversibleRectangle(ptSelectionStart, ptSelectionEnd);

                // Clear the selection rectangle
                ptSelectionEnd.X = -1;
                ptSelectionEnd.Y = -1;
                ptSelectionStart.X = -1;
                ptSelectionStart.Y = -1;

                // Stop selecting
                shiftSelecting = false;

                // Position of the panel to the screen
                Point ptPbFull = PointToScreen(pbFull.Location);

                // Zoom to my selection
                drawing.ZoomToSelection(rect, ptPbFull);

                // Refresh my screen & update my preview panel
                pbFull.Refresh();
                UpdatePanels(true);
            }
            else
            {
                // Stop dragging and update my panels
                drawing.EndDrag();
                UpdatePanels(true);

                // Fancy cursor
                if (dragCursor != null)
                {
                    pbFull.Cursor = dragCursor;
                }
            }
        }

        private void pbFull_MouseMove(object sender, MouseEventArgs e)
        {
            // Am I dragging or selecting?
            if (shiftSelecting == true)
            {
                // Keep selecting
                ptSelectionEnd.X = e.X;
                ptSelectionEnd.Y = e.Y;
                
                Rectangle pbFullRect = new Rectangle(0, 0, pbFull.Width - 1, pbFull.Height - 1);

                // Am I still selecting within my panel?
                if (pbFullRect.Contains(new Point(e.X, e.Y)))
                {
                    // If so, draw my Rubber Band Rectangle!
                    Rectangle rect = CalculateReversibleRectangle(ptSelectionStart, ptSelectionEnd);
                    DrawReversibleRectangle(rect);
                }
            }
            else
            {
                // Keep dragging
                drawing.Drag(new Point(e.X, e.Y));
                if (drawing.IsDragging)
                {
                    UpdatePanels(false);
                }
                else
                {
                    // I'm not dragging OR selecting
                    // Make sure if left or right shift is pressed to change cursor

                    if (this.IsKeyPressed(0xA0) || this.IsKeyPressed(0xA1) || selectMode == true) 
                    {
                        // Fancy Cursor
                        if (pbFull.Cursor != Cursors.Cross)
                        {
                            pbFull.Cursor = Cursors.Cross;
                        }
                    }
                    else
                    {
                        // Fancy Cursor
                        if (pbFull.Cursor != dragCursor)
                        {
                            pbFull.Cursor = dragCursor;
                        }
                    }
                }
            }
        }

        private void KP_ImageViewerV2_MouseWheel(object sender, MouseEventArgs e)
        {
            drawing.Scroll(sender, e);

            if (drawing.Image != null)
            {
                if (e.Delta < 0)
                {
                    OnZoom(new ImageViewerZoomEventArgs(drawing.Zoom, KpZoom.ZoomOut));
                }
                else
                {
                    OnZoom(new ImageViewerZoomEventArgs(drawing.Zoom, KpZoom.ZoomIn));
                }
            }

            UpdatePanels(true);
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();

            openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.gif;*.bmp;*.png;*.tif;*.tiff;*.wmf;*.emf|JPEG Files (*.jpg)|*.jpg;*.jpeg|GIF Files (*.gif)|*.gif|BMP Files (*.bmp)|*.bmp|PNG Files (*.png)|*.png|TIF files (*.tif;*.tiff)|*.tif;*.tiff|EMF/WMF Files (*.wmf;*.emf)|*.wmf;*.emf|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                this.ImagePath = openFileDialog.FileName;
            }

            UpdatePanels(true);
        }

        private void btnRotate270_Click(object sender, EventArgs e)
        {
            if (drawing != null)
            {
                drawing.Rotate270();

                // AfterRotation Event
                OnRotation(new ImageViewerRotationEventArgs(drawing.Rotation));
                UpdatePanels(true);
                ToggleMultiPage();
            }
        }

        private void btnRotate90_Click(object sender, EventArgs e)
        {
            if (drawing != null)
            {
                drawing.Rotate90();

                // AfterRotation Event
                OnRotation(new ImageViewerRotationEventArgs(drawing.Rotation));
                UpdatePanels(true);
                ToggleMultiPage();
            }
        }

        public void Rotate90()
        {
            if (drawing != null)
            {
                drawing.Rotate90();

                // AfterRotation Event
                OnRotation(new ImageViewerRotationEventArgs(drawing.Rotation));
                UpdatePanels(true);
                ToggleMultiPage();
            }
        }

        public void Rotate180()
        {
            if (drawing != null)
            {
                drawing.Rotate180();

                // AfterRotation Event
                OnRotation(new ImageViewerRotationEventArgs(drawing.Rotation));
                UpdatePanels(true);
                ToggleMultiPage();
            }
        }

        public void Rotate270()
        {
            if (drawing != null)
            {
                drawing.Rotate270();

                // AfterRotation Event
                OnRotation(new ImageViewerRotationEventArgs(drawing.Rotation));
                UpdatePanels(true);
                ToggleMultiPage();
            }
        }

        private void btnZoomOut_Click(object sender, EventArgs e)
        {
            drawing.ZoomOut();

            // AfterZoom Event
            if (drawing.Image != null)
            {
                OnZoom(new ImageViewerZoomEventArgs(drawing.Zoom, KpZoom.ZoomOut));
            }
            UpdatePanels(true);
        }

        private void btnZoomIn_Click(object sender, EventArgs e)
        {
            drawing.ZoomIn();

            // AfterZoom Event
            if (drawing.Image != null)
            {
                OnZoom(new ImageViewerZoomEventArgs(drawing.Zoom, KpZoom.ZoomIn));
            }
            UpdatePanels(true);
        }

        private void btnFitToScreen_Click(object sender, EventArgs e)
        {
            drawing.FitToScreen();
            UpdatePanels(true);
        }

        private void cbZoom_SelectedIndexChanged(object sender, EventArgs e)
        {
            double zoom = (cbZoom.SelectedIndex + 1) * 0.25;
            double originalZoom = drawing.Zoom;

            if (drawing.Zoom != zoom)
            {
                drawing.SetZoom(zoom);

                if (drawing.Image != null)
                {
                    if (zoom > originalZoom)
                    {
                        OnZoom(new ImageViewerZoomEventArgs(drawing.Zoom, KpZoom.ZoomIn));
                    }
                    else
                    {
                        OnZoom(new ImageViewerZoomEventArgs(drawing.Zoom, KpZoom.ZoomOut));
                    }
                }

                UpdatePanels(true);
            }
        }

        private void UpdatePanels(bool updatePreview)
        {
            if (drawing.CurrentSize.Width > 0 && drawing.OriginalSize.Width > 0)
            {
                // Make sure panel is up to date
                pbFull.Refresh();

                // Calculate zoom
                double zoom = Math.Round(((double)drawing.CurrentSize.Width / (double)drawing.OriginalSize.Width), 2);

                // Display zoom in percentages
                cbZoom.Text = (int)(zoom * 100) + "%";

                if (updatePreview && drawing.PreviewImage != null && pbPanel.Visible == true)
                {
                    // No memory leaks here
                    if (preview != null)
                    {
                        preview.Dispose();
                        preview = null;
                    }

                    // New preview
                    preview = new Bitmap(drawing.PreviewImage.Size.Width, drawing.PreviewImage.Size.Height);

                    // Make sure panel is the same size as the bitmap
                    if (pbPanel.Size != drawing.PreviewImage.Size)
                    {
                        pbPanel.Size = drawing.PreviewImage.Size;
                    }

                    // New Graphics from the new bitmap we created (Empty)
                    using (Graphics g = Graphics.FromImage(preview))
                    {
                        // Draw the image on the bitmap
                        g.DrawImage(drawing.PreviewImage, 0, 0, drawing.PreviewImage.Size.Width, drawing.PreviewImage.Size.Height);

                        double ratioX = (double)drawing.PreviewImage.Size.Width / (double)drawing.CurrentSize.Width;
                        double ratioY = (double)drawing.PreviewImage.Size.Height / (double)drawing.CurrentSize.Height;

                        double boxWidth = pbFull.Width * ratioX;
                        double boxHeight = pbFull.Height * ratioY;
                        double positionX = ((drawing.BoundingBox.X - (drawing.BoundingBox.X * 2)) * ratioX);
                        double positionY = ((drawing.BoundingBox.Y - (drawing.BoundingBox.Y * 2)) * ratioY);

                        // Making the red pen
                        Pen pen = new Pen(Color.Red, 1);

                        if (boxHeight >= drawing.PreviewImage.Size.Height)
                        {
                            boxHeight = drawing.PreviewImage.Size.Height - 1;
                        }
                        else if ((boxHeight + positionY) > drawing.PreviewImage.Size.Height)
                        {
                            boxHeight = drawing.PreviewImage.Size.Height - (positionY);
                        }

                        if (boxWidth >= drawing.PreviewImage.Size.Width)
                        {
                            boxWidth = drawing.PreviewImage.Size.Width - 1;
                        }
                        else if ((boxWidth + positionX) > drawing.PreviewImage.Size.Width)
                        {
                            boxWidth = drawing.PreviewImage.Size.Width - (positionX);
                        }

                        // Draw the rectangle on the bitmap
                        g.DrawRectangle(pen, new Rectangle((int)positionX, (int)positionY, (int)boxWidth, (int)boxHeight));
                    }

                    // Display the bitmap
                    pbPanel.Image = preview;
                }
            }
        }

        private void pbPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (panelDragging == false)
            {
                drawing.JumpToOrigin(e.X, e.Y, pbPanel.Width, pbPanel.Height, pbFull.Width, pbFull.Height);
                UpdatePanels(true);

                panelDragging = true;
            }
        }

        private void pbFull_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            drawing.JumpToOrigin(e.X + (drawing.BoundingBox.X - (drawing.BoundingBox.X * 2)), e.Y + (drawing.BoundingBox.Y - (drawing.BoundingBox.Y * 2)), pbFull.Width, pbFull.Height);
            UpdatePanels(true);
        }

        private void pbFull_MouseHover(object sender, EventArgs e)
        {
            // Left shift or Right shift!
            if (this.IsKeyPressed(0xA0) || this.IsKeyPressed(0xA1))
            {
                // Fancy cursor
                pbFull.Cursor = Cursors.Cross;
            }
            else
            {
                // Fancy cursor if not dragging
                if (!drawing.IsDragging)
                {
                    pbFull.Cursor = dragCursor;
                }
            }
        }

        private void KpImageViewer_Click(object sender, EventArgs e)
        {
            FocusOnMe();
        }

        private void pbFull_Click(object sender, EventArgs e)
        {
            FocusOnMe();
        }

        private void pbPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (panelDragging)
            {
                drawing.JumpToOrigin(e.X, e.Y, pbPanel.Width, pbPanel.Height, pbFull.Width, pbFull.Height);
                UpdatePanels(true);
            }
        }

        private void pbPanel_MouseUp(object sender, MouseEventArgs e)
        {
            panelDragging = false;
        }

        private void pbFull_MouseEnter(object sender, EventArgs e)
        {
            if (this.IsKeyPressed(0xA0) || this.IsKeyPressed(0xA1) || selectMode == true)
            {
                pbFull.Cursor = Cursors.Cross;
            }
            else
            {
                if (dragCursor != null)
                {
                    pbFull.Cursor = dragCursor;
                }
            }
        }

        private void pbFull_MouseLeave(object sender, EventArgs e)
        {
            pbFull.Cursor = Cursors.Default;
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            if (this.ShowPreview)
            {
                this.ShowPreview = false;
            }
            else
            {
                this.ShowPreview = true;
            }
        }

        private void cbZoom_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                // If it's not a digit, delete or backspace then make sure the input is being handled with. (Suppressed)
                if (!Char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Delete && e.KeyChar != (char)Keys.Back)
                {
                    // If enter is pressed apply the entered zoom
                    if (e.KeyChar == (char)Keys.Return)
                    {
                        int zoom = 0;

                        // Make sure the percent sign is out of the cbZoom.Text
                        int.TryParse(cbZoom.Text.Replace("%", ""), out zoom);

                        // If zoom is higher than zero
                        if (zoom > 0)
                        {
                            // Make it a double!
                            double zoomDouble = (double)zoom / (double)100;

                            drawing.SetZoom(zoomDouble);
                            UpdatePanels(true);

                            btnZoomIn.Focus();
                        }
                    }

                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("ImageViewer error: " + ex.ToString());
            }
        }

        private Rectangle CalculateReversibleRectangle(Point ptSelectStart, Point ptSelectEnd)
        {
            Rectangle rect = new Rectangle();

            ptSelectStart = pbFull.PointToScreen(ptSelectStart);
            ptSelectEnd = pbFull.PointToScreen(ptSelectEnd);

            if (ptSelectStart.X < ptSelectEnd.X)
            {
                rect.X = ptSelectStart.X;
                rect.Width = ptSelectEnd.X - ptSelectStart.X;
            }
            else
            {
                rect.X = ptSelectEnd.X;
                rect.Width = ptSelectStart.X - ptSelectEnd.X;
            }
            if (ptSelectStart.Y < ptSelectEnd.Y)
            {
                rect.Y = ptSelectStart.Y;
                rect.Height = ptSelectEnd.Y - ptSelectStart.Y;
            }
            else
            {
                rect.Y = ptSelectEnd.Y;
                rect.Height = ptSelectStart.Y - ptSelectEnd.Y;
            }

            return rect;
        }

        private void DrawReversibleRectangle(Rectangle rect)
        {
            pbFull.Refresh();
            ControlPaint.DrawReversibleFrame(rect, Color.LightGray, FrameStyle.Dashed);
        }

        private void pbFull_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                // Get The file(s) you dragged into an array. (We'll just pick the first image anyway)
                string[] FileList = (string[])e.Data.GetData(DataFormats.FileDrop, false);

                Image newBmp = null;

                for (int f = 0; f < FileList.Length; f++)
                {
                    // Make sure the file exists!
                    if (System.IO.File.Exists(FileList[f]))
                    {
                        string ext = (System.IO.Path.GetExtension(FileList[f])).ToLower();

                        // Checking the extensions to be Image formats
                        if (ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".wmf" || ext == ".emf" || ext == ".bmp" || ext == ".png" || ext == ".tif" || ext == ".tiff")
                        {
                            try
                            {
                                // Try to load it into a bitmap
                                //newBmp = Bitmap.FromFile(FileList[f]);
                                newBmp = new Bitmap(FileList[f]);
                                this.Image = (Bitmap)newBmp;

                                // If succeeded stop the loop
                                break;
                            }
                            catch
                            {
                                // Not an image?
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("ImageViewer error: " + ex.ToString());
            }
        }

        private void pbFull_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    // Drop the file
                    e.Effect = DragDropEffects.Copy;
                }
                else
                {
                    // I'm not going to accept this unknown format!
                    e.Effect = DragDropEffects.None;
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("ImageViewer error: " + ex.ToString());
            }
        }

        private void btnMode_Click(object sender, EventArgs e)
        {
            if (selectMode == false)
            {
                selectMode = true;
                this.btnMode.Image = KaiwaProjects.Properties.Resources.btnDrag;
            }
            else
            {
                selectMode = false;
                this.btnMode.Image = KaiwaProjects.Properties.Resources.btnSelect;
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            drawing.NextPage();
            tbNavigation.Text = (drawing.CurrentPage + 1).ToString();

            pbFull.Refresh();
            UpdatePanels(true);
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            drawing.PreviousPage();
            tbNavigation.Text = (drawing.CurrentPage + 1).ToString();

            pbFull.Refresh();
            UpdatePanels(true);
        }

        private void tbNavigation_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                // If it's not a digit, delete or backspace then make sure the input is being handled with. (Suppressed)
                if (!Char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Delete && e.KeyChar != (char)Keys.Back)
                {
                    // If enter is pressed apply the entered zoom
                    if (e.KeyChar == (char)Keys.Return)
                    {
                        int page = 0;

                        int.TryParse(tbNavigation.Text, out page);
                        
                        // If zoom is higher than zero
                        if (page > 0 && page <= drawing.Pages)
                        {
                            drawing.SetPage(page);
                            UpdatePanels(true);

                            btnZoomIn.Focus();
                        }
                        else
                        {
                            tbNavigation.Text = drawing.CurrentPage.ToString();
                        }
                    }

                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("ImageViewer error: " + ex.ToString());
            }
        }
    }

    public class ImageViewerRotationEventArgs : EventArgs
    {
        private int rotation;
        public int Rotation
        {
            get { return rotation; }
        }

        public ImageViewerRotationEventArgs(int rotation)
        {
            this.rotation = rotation;
        }
    }

    public class ImageViewerZoomEventArgs : EventArgs
    {
        private int zoom;
        public int Zoom
        {
            get { return zoom; }
        }

        private KpZoom inOut;
        public KpZoom InOut
        {
            get { return inOut; }
        }

        public ImageViewerZoomEventArgs(double zoom, KpZoom inOut)
        {
            this.zoom = Convert.ToInt32(Math.Round((zoom * 100), 0));
            this.inOut = inOut;
        }
    }
}
