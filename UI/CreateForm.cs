using System;
using System.Drawing;
using System.Windows.Forms;
using Lab1_4Sem.Services;

namespace Lab1_4Sem.UI
{
    public class CreateForm : Form
    {
        private readonly TextBox _tbName;
        private readonly NumericUpDown _nudLen;
        private readonly TextBox _tbSpec;
        private readonly Button _btnOk;
        private readonly ProductFileService _service;

        public CreateForm(ProductFileService service)
        {
            _service = service;
            Text = "Создать файлы";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 190);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblName = new Label
            {
                Text = "Имя файла (без расширения .prd):",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _tbName = new TextBox
            {
                Dock = DockStyle.Fill
            };

            var lblLen = new Label
            {
                Text = "Максимальная длина имени (в байтах):",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _nudLen = new NumericUpDown
            {
                Minimum = 4,
                Maximum = 200,
                Value = 32,
                Width = 120,
                Anchor = AnchorStyles.Left
            };

            var lblSpec = new Label
            {
                Text = "Файл спецификаций (.prs, опционально):",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _tbSpec = new TextBox
            {
                Dock = DockStyle.Fill
            };

            _btnOk = new Button
            {
                Text = "Создать",
                Width = 120,
                Anchor = AnchorStyles.Left
            };
            _btnOk.Click += (s, e) => OnCreate();
            AcceptButton = _btnOk;

            layout.Controls.Add(lblName, 0, 0);
            layout.Controls.Add(_tbName, 1, 0);
            layout.Controls.Add(lblLen, 0, 1);
            layout.Controls.Add(_nudLen, 1, 1);
            layout.Controls.Add(lblSpec, 0, 2);
            layout.Controls.Add(_tbSpec, 1, 2);
            layout.Controls.Add(_btnOk, 0, 3);

            Controls.Add(layout);
        }

        private void OnCreate()
        {
            var name = _tbName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите имя файла.");
                return;
            }

            _service.Create(name, (int)_nudLen.Value, string.IsNullOrWhiteSpace(_tbSpec.Text) ? null : _tbSpec.Text.Trim());
            if (_service.LastOperationSucceeded)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show(_service.LastOperationMessage);
            }
        }
    }
}
