using System;
using System.Drawing;
using System.Windows.Forms;
using Lab1_4Sem.Models;
using Lab1_4Sem.Services;

namespace Lab1_4Sem.UI
{
    public class EditComponentForm : Form
    {
        private readonly TextBox _tbName;
        private readonly ComboBox _cbType;
        private readonly Button _btnOk;
        private readonly ProductFileService _service;
        private readonly string _originalName;

        public EditComponentForm(ProductFileService service, string existingName, ComponentType existingType)
        {
            _service = service;
            _originalName = existingName;
            Text = "Изменить компонент";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 150);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblName = new Label
            {
                Text = "Имя компонента:",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _tbName = new TextBox { Dock = DockStyle.Fill };

            var lblType = new Label
            {
                Text = "Тип компонента:",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _cbType = new ComboBox
            {
                Dock = DockStyle.Left,
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cbType.Items.AddRange(new object[] { ComponentType.Изделие, ComponentType.Узел, ComponentType.Деталь });

            _btnOk = new Button
            {
                Text = "Сохранить",
                Width = 120,
                Anchor = AnchorStyles.Left
            };
            _btnOk.Click += (s, e) => OnSave();
            AcceptButton = _btnOk;

            _tbName.Text = existingName;
            _cbType.SelectedItem = existingType;

            layout.Controls.Add(lblName, 0, 0);
            layout.Controls.Add(_tbName, 1, 0);
            layout.Controls.Add(lblType, 0, 1);
            layout.Controls.Add(_cbType, 1, 1);
            layout.Controls.Add(_btnOk, 0, 2);

            Controls.Add(layout);
        }

        private void OnSave()
        {
            var newName = _tbName.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Введите имя компонента.");
                return;
            }

            if (newName != _originalName)
            {
                if (!_service.RenameComponent(_originalName, newName))
                {
                    MessageBox.Show(_service.LastOperationMessage);
                    return;
                }
            }

            var type = (ComponentType)_cbType.SelectedItem!;
            _service.SetComponentType(newName, type);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
