using System.Drawing;
using System.Windows.Forms;

namespace BfntConverterApp.Rikako
{
    internal partial class RikakoTilemapViewerForm
    {
        private RichTextBox richTextBox1 = null!;
        private TableLayoutPanel tableLayoutPanel1 = null!;
        private Panel panel1 = null!;
        private PictureBox pictureBox2 = null!;
        private Button btnPrevSection = null!;
        private Button btnNextSection = null!;
        private Label lblSectionNumber = null!;
        private MenuStrip menuStrip1 = null!;
        private ToolStripMenuItem fileMenu = null!;
        private ToolStripMenuItem helpMenu = null!;

        private void InitializeComponent()
        {
            richTextBox1 = new RichTextBox();
            tableLayoutPanel1 = new TableLayoutPanel();
            pictureBox2 = new PictureBox();
            panel1 = new Panel();
            btnPrevSection = new Button();
            lblSectionNumber = new Label();
            btnNextSection = new Button();
            menuStrip1 = new MenuStrip();
            fileMenu = new ToolStripMenuItem();
            helpMenu = new ToolStripMenuItem();
            tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox2).BeginInit();
            panel1.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // richTextBox1
            // 
            richTextBox1.AcceptsTab = true;
            richTextBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            richTextBox1.Font = new Font("Consolas", 10F);
            richTextBox1.Location = new Point(16, 16);
            richTextBox1.Margin = new Padding(2);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(967, 161);
            richTextBox1.TabIndex = 4;
            richTextBox1.Text = "";
            richTextBox1.WordWrap = false;
            // 
            // tableLayoutPanel1
            // 
            tableLayoutPanel1.ColumnCount = 1;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tableLayoutPanel1.Controls.Add(pictureBox2, 0, 0);
            tableLayoutPanel1.Controls.Add(panel1, 0, 1);
            tableLayoutPanel1.Dock = DockStyle.Fill;
            tableLayoutPanel1.Location = new Point(0, 28);
            tableLayoutPanel1.Margin = new Padding(2);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            tableLayoutPanel1.Size = new Size(1010, 574);
            tableLayoutPanel1.TabIndex = 6;
            // 
            // pictureBox2
            // 
            pictureBox2.BackColor = Color.White;
            pictureBox2.Dock = DockStyle.Fill;
            pictureBox2.Location = new Point(2, 2);
            pictureBox2.Margin = new Padding(2);
            pictureBox2.Name = "pictureBox2";
            pictureBox2.Size = new Size(1006, 340);
            pictureBox2.SizeMode = PictureBoxSizeMode.CenterImage;
            pictureBox2.TabIndex = 2;
            pictureBox2.TabStop = false;
            // 
            // panel1
            // 
            panel1.Controls.Add(richTextBox1);
            panel1.Controls.Add(btnPrevSection);
            panel1.Controls.Add(lblSectionNumber);
            panel1.Controls.Add(btnNextSection);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(2, 346);
            panel1.Margin = new Padding(2);
            panel1.Name = "panel1";
            panel1.Size = new Size(1006, 228);
            panel1.TabIndex = 0;
            // 
            // btnPrevSection
            // 
            btnPrevSection.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnPrevSection.Location = new Point(16, 194);
            btnPrevSection.Name = "btnPrevSection";
            btnPrevSection.Size = new Size(90, 27);
            btnPrevSection.TabIndex = 5;
            btnPrevSection.Text = "< Previous";
            btnPrevSection.UseVisualStyleBackColor = true;
            btnPrevSection.Click += PreviousSection_Click;
            // 
            // lblSectionNumber
            // 
            lblSectionNumber.Anchor = AnchorStyles.Bottom;
            lblSectionNumber.Location = new Point(442, 197);
            lblSectionNumber.Name = "lblSectionNumber";
            lblSectionNumber.Size = new Size(120, 20);
            lblSectionNumber.TabIndex = 6;
            lblSectionNumber.Text = "Section: 0/0";
            lblSectionNumber.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // btnNextSection
            // 
            btnNextSection.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnNextSection.Location = new Point(881, 194);
            btnNextSection.Name = "btnNextSection";
            btnNextSection.Size = new Size(90, 27);
            btnNextSection.TabIndex = 7;
            btnNextSection.Text = "Next >";
            btnNextSection.UseVisualStyleBackColor = true;
            btnNextSection.Click += NextSection_Click;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileMenu, helpMenu });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1010, 28);
            menuStrip1.TabIndex = 0;
            // 
            // fileMenu
            // 
            fileMenu.Name = "fileMenu";
            fileMenu.Size = new Size(46, 24);
            fileMenu.Text = "文件(&F)";
            fileMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("载入地图文本(&M)", null, LoadMap_Click),
                new ToolStripSeparator(),
                new ToolStripMenuItem("关闭(&X)", null, Exit_Click)
            });
            // 
            // helpMenu
            // 
            helpMenu.Name = "helpMenu";
            helpMenu.Size = new Size(69, 24);
            helpMenu.Text = "帮助(&H)";
            helpMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                new ToolStripMenuItem("关于(&A)", null, About_Click)
            });
            // 
            // RikakoTilemapViewerForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1010, 602);
            Controls.Add(tableLayoutPanel1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Margin = new Padding(2);
            MinimumSize = new Size(804, 489);
            Name = "RikakoTilemapViewerForm";
            Text = "Rikako 地图块查看器";
            tableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox2).EndInit();
            panel1.ResumeLayout(false);
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
