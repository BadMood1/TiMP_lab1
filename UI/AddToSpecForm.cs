using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Lab1_4Sem.Services;

namespace Lab1_4Sem.UI
{
    public class AddToSpecForm : Form
    {
        private readonly ComboBox _cbComponent;
        private readonly ComboBox _cbPart;
        private readonly NumericUpDown _nudQty;
        private readonly Button _btnOk;
        private readonly ProductFileService _service;

        public AddToSpecForm(ProductFileService service, string? componentName = null)
        {
            _service = service;
            Text = "Добавить в спецификацию";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 190);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var lblComponent = new Label
            {
                Text = "Родительский компонент:",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _cbComponent = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var lblPart = new Label
            {
                Text = "Добавляемый компонент:",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _cbPart = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            var lblQty = new Label
            {
                Text = "Кратность (кол-во):",
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            _nudQty = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 100,
                Value = 1,
                Width = 120,
                Anchor = AnchorStyles.Left
            };

            _btnOk = new Button
            {
                Text = "Добавить",
                Width = 120,
                Anchor = AnchorStyles.Left
            };
            _btnOk.Click += (s, e) => OnAdd();
            AcceptButton = _btnOk;

            var names = _service.GetAllActiveProducts()
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            _cbComponent.Items.AddRange(names.Cast<object>().ToArray());
            _cbPart.Items.AddRange(names.Cast<object>().ToArray());

            if (!string.IsNullOrWhiteSpace(componentName))
            {
                _cbComponent.SelectedItem = componentName;
                _cbComponent.Enabled = false;
            }
            else if (_cbComponent.Items.Count > 0)
            {
                _cbComponent.SelectedIndex = 0;
            }

            if (_cbPart.Items.Count > 0)
                _cbPart.SelectedIndex = 0;

            layout.Controls.Add(lblComponent, 0, 0);
            layout.Controls.Add(_cbComponent, 1, 0);
            layout.Controls.Add(lblPart, 0, 1);
            layout.Controls.Add(_cbPart, 1, 1);
            layout.Controls.Add(lblQty, 0, 2);
            layout.Controls.Add(_nudQty, 1, 2);
            layout.Controls.Add(_btnOk, 0, 3);

            Controls.Add(layout);
        }

        private void OnAdd()
        {
            if (_cbComponent.SelectedItem == null || _cbPart.SelectedItem == null)
            {
                MessageBox.Show("Выберите оба компонента.");
                return;
            }

            var component = _cbComponent.SelectedItem.ToString()!;
            var part = _cbPart.SelectedItem.ToString()!;

            _service.AddToSpecification(component, part, (short)_nudQty.Value);
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
