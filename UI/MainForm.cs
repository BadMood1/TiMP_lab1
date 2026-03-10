using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Lab1_4Sem.Models;
using Lab1_4Sem.Services;

namespace Lab1_4Sem.UI
{
    public class MainForm : Form
    {
        private MenuStrip _menu;
        private ToolStripMenuItem _fileMenu;
        private ToolStripMenuItem _componentsMenu;
        private ToolStripMenuItem _specMenu;
        private TabControl _tabs;
        private TabPage _tabComponents;
        private TabPage _tabSpecs;

        private ListView _componentsList;
        private TextBox _tbEditName;
        private ComboBox _cbEditType;
        private Button _btnEdit;
        private Button _btnAddComponent;
        private Button _btnDeleteComponent;
        private Button _btnSave;
        private Button _btnCancel;
        private Button _btnRefreshComponents;

        private TreeView _specTree;
        private Button _btnAddSpec;
        private Button _btnDeleteSpec;
        private Button _btnRefreshSpec;

        private ProductFileService _service;
        private string? _currentProductPath;
        private string? _currentSpecPath;
        private string? _backupProductPath;
        private string? _backupSpecPath;
        private bool _hasPendingChanges;

        public MainForm()
        {
            Text = "Организация многосвязных структур";
            Width = 900;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;

            _service = new ProductFileService();

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            _menu = new MenuStrip();
            _fileMenu = new ToolStripMenuItem("Файл");
            var open = new ToolStripMenuItem("Открыть", null, OnOpen);
            var create = new ToolStripMenuItem("Создать", null, OnCreate);
            var exit = new ToolStripMenuItem("Выход", null, (s, e) => Close());
            _fileMenu.DropDownItems.AddRange(new ToolStripItem[] { open, create, new ToolStripSeparator(), exit });

            _componentsMenu = new ToolStripMenuItem("Компоненты");
            _componentsMenu.Click += (s, e) => _tabs.SelectedTab = _tabComponents;

            _specMenu = new ToolStripMenuItem("Спецификация");
            _specMenu.Click += (s, e) => _tabs.SelectedTab = _tabSpecs;

            _menu.Items.Add(_fileMenu);
            _menu.Items.Add(_componentsMenu);
            _menu.Items.Add(_specMenu);
            Controls.Add(_menu);

            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabComponents = new TabPage("Компоненты");
            _tabSpecs = new TabPage("Спецификация");
            _tabs.TabPages.Add(_tabComponents);
            _tabs.TabPages.Add(_tabSpecs);
            Controls.Add(_tabs);

            BuildComponentsTab();
            BuildSpecsTab();
        }

        private void BuildComponentsTab()
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));

            _componentsList = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                Dock = DockStyle.Fill
            };
            _componentsList.Columns.Add("Наименование", 260);
            _componentsList.Columns.Add("Тип", 120);
            _componentsList.SelectedIndexChanged += (s, e) => SyncEditorWithSelection();
            _componentsList.DoubleClick += (s, e) => OpenEditDialog();
            table.Controls.Add(_componentsList, 0, 0);

            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            _btnAddComponent = new Button { Text = "Добавить", Width = 160 };
            _btnAddComponent.Click += (s, e) => OnAddComponent();

            _btnDeleteComponent = new Button { Text = "Удалить", Width = 160 };
            _btnDeleteComponent.Click += (s, e) => OnDeleteComponent();

            _btnSave = new Button { Text = "Сохранить", Width = 160 };
            _btnSave.Click += (s, e) => OnSaveChanges();

            _btnCancel = new Button { Text = "Отменить", Width = 160 };
            _btnCancel.Click += (s, e) => OnCancelChanges();

            _btnRefreshComponents = new Button { Text = "Обновить", Width = 160 };
            _btnRefreshComponents.Click += (s, e) => RefreshAll();

            buttonsPanel.Controls.AddRange(new Control[]
            {
                _btnAddComponent, _btnDeleteComponent, _btnSave, _btnCancel, _btnRefreshComponents
            });
            table.Controls.Add(buttonsPanel, 1, 0);

            var editPanel = new Panel { Dock = DockStyle.Fill };
            var lblName = new Label { Text = "Наименование", Left = 10, Top = 10, AutoSize = true };
            _tbEditName = new TextBox { Left = 120, Top = 7, Width = 240 };
            var lblType = new Label { Text = "Тип", Left = 380, Top = 10, AutoSize = true };
            _cbEditType = new ComboBox
            {
                Left = 420,
                Top = 7,
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cbEditType.Items.AddRange(new object[] { ComponentType.Изделие, ComponentType.Узел, ComponentType.Деталь });
            _cbEditType.SelectedIndex = 0;

            _btnEdit = new Button { Text = "Изменить", Left = 600, Top = 6, Width = 120 };
            _btnEdit.Click += (s, e) => OnEditComponentInline();

            editPanel.Controls.AddRange(new Control[] { lblName, _tbEditName, lblType, _cbEditType, _btnEdit });
            table.Controls.Add(editPanel, 0, 1);
            table.SetColumnSpan(editPanel, 2);

            _tabComponents.Controls.Add(table);
        }

        private void BuildSpecsTab()
        {
            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

            _specTree = new TreeView { Dock = DockStyle.Fill };
            _specTree.NodeMouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    _specTree.SelectedNode = e.Node;
                    ShowSpecContextMenu(e.Node, e.Location);
                }
            };
            table.Controls.Add(_specTree, 0, 0);

            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };

            _btnAddSpec = new Button { Text = "Добавить", Width = 160 };
            _btnAddSpec.Click += (s, e) => OnAddSpec();

            _btnDeleteSpec = new Button { Text = "Удалить", Width = 160 };
            _btnDeleteSpec.Click += (s, e) => OnDeleteSpec();

            _btnRefreshSpec = new Button { Text = "Обновить", Width = 160 };
            _btnRefreshSpec.Click += (s, e) => RefreshAll();

            buttonsPanel.Controls.AddRange(new Control[] { _btnAddSpec, _btnDeleteSpec, _btnRefreshSpec });
            table.Controls.Add(buttonsPanel, 1, 0);

            _tabSpecs.Controls.Add(table);
        }

        private void OnOpen(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog();
            dlg.Filter = "Product files (*.prd)|*.prd|All files|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _service.Open(dlg.FileName);
                if (!_service.LastOperationSucceeded)
                {
                    MessageBox.Show(_service.LastOperationMessage);
                    return;
                }

                _currentProductPath = _service.ProductFilePath;
                _currentSpecPath = ResolveSpecPath();
                CreateBackup();
                _hasPendingChanges = false;
                RefreshAll();
            }
        }

        private void OnCreate(object? sender, EventArgs e)
        {
            using var f = new CreateForm(_service);
            if (f.ShowDialog() == DialogResult.OK && _service.IsOpen)
            {
                _currentProductPath = _service.ProductFilePath;
                _currentSpecPath = ResolveSpecPath();
                CreateBackup();
                _hasPendingChanges = false;
                RefreshAll();
            }
        }

        private void OnAddComponent()
        {
            if (!EnsureOpen())
                return;

            using var f = new AddComponentForm(_service);
            if (f.ShowDialog() == DialogResult.OK)
            {
                MarkChanged();
                RefreshAll();
            }
        }

        private void OpenEditDialog()
        {
            if (!EnsureOpen())
                return;

            if (_componentsList.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите компонент в списке.");
                return;
            }

            var name = _componentsList.SelectedItems[0].Text;
            var item = _service.GetAllActiveProducts().FirstOrDefault(p => p.Name == name);
            if (string.IsNullOrEmpty(item.Name))
            {
                MessageBox.Show("Компонент не найден.");
                return;
            }

            using var f = new EditComponentForm(_service, item.Name, item.Type);
            if (f.ShowDialog() == DialogResult.OK)
            {
                MarkChanged();
                RefreshAll();
            }
        }

        private void OnEditComponentInline()
        {
            if (!EnsureOpen())
                return;

            if (_componentsList.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите компонент в списке.");
                return;
            }

            var oldName = _componentsList.SelectedItems[0].Text;
            var newName = _tbEditName.Text.Trim();
            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Введите новое имя.");
                return;
            }

            if (newName != oldName)
            {
                if (!_service.RenameComponent(oldName, newName))
                {
                    MessageBox.Show("Переименование не выполнено (возможно, имя занято).");
                    return;
                }
            }

            var type = (ComponentType)_cbEditType.SelectedItem!;
            _service.SetComponentType(newName, type);

            MarkChanged();
            RefreshAll();
        }

        private void OnDeleteComponent()
        {
            if (!EnsureOpen())
                return;

            if (_componentsList.SelectedItems.Count == 0)
            {
                MessageBox.Show("Выберите компонент в списке.");
                return;
            }

            var name = _componentsList.SelectedItems[0].Text;
            _service.DeleteComponent(name);
            if (_service.LastOperationSucceeded)
            {
                MarkChanged();
                RefreshAll();
            }
            else
            {
                MessageBox.Show(_service.LastOperationMessage);
            }
        }

        private void OnAddSpec()
        {
            if (!EnsureOpen())
                return;

            var componentName = ResolveComponentNameForSpec();
            using var f = new AddToSpecForm(_service, componentName);
            if (f.ShowDialog() == DialogResult.OK)
            {
                MarkChanged();
                RefreshAll();
            }
        }

        private void OnDeleteSpec()
        {
            if (!EnsureOpen())
                return;

            if (_specTree.SelectedNode == null || _specTree.SelectedNode.Tag is not string tag)
            {
                MessageBox.Show("Выберите запись в спецификации.");
                return;
            }

            if (!TryDeleteSpecByTag(tag))
            {
                MessageBox.Show("Нельзя удалить этот элемент.");
                return;
            }

            if (_service.LastOperationSucceeded)
            {
                MarkChanged();
                RefreshAll();
            }
            else
            {
                MessageBox.Show(_service.LastOperationMessage);
            }
        }

        private void OnSaveChanges()
        {
            if (!_hasPendingChanges)
            {
                MessageBox.Show("Нет несохраненных изменений.");
                return;
            }

            if (_currentProductPath == null)
            {
                MessageBox.Show("Файл не открыт.");
                return;
            }

            CreateBackup();
            _hasPendingChanges = false;
            MessageBox.Show("Изменения сохранены.");
        }

        private void OnCancelChanges()
        {
            if (!_hasPendingChanges)
            {
                MessageBox.Show("Нет изменений для отмены.");
                return;
            }

            if (MessageBox.Show("Отменить все несохраненные изменения?", "Подтверждение", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            RestoreBackup();
            _hasPendingChanges = false;
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshComponents();
            RefreshSpecs();
        }

        private void RefreshComponents()
        {
            _componentsList.Items.Clear();

            try
            {
                foreach (var p in _service.GetAllActiveProducts())
                {
                    var lv = new ListViewItem(new[] { p.Name, p.Type.ToString() });
                    _componentsList.Items.Add(lv);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при обновлении списка компонентов: " + ex.Message);
            }
        }

        private void RefreshSpecs()
        {
            _specTree.Nodes.Clear();

            try
            {
                var products = _service.GetAllActiveProducts().ToList();
                var referencedNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in products)
                {
                    foreach (var entry in _service.GetSpecificationEntries(p.Name))
                        referencedNames.Add(entry.PartName);
                }

                var roots = products.Where(p => !referencedNames.Contains(p.Name)).ToList();
                if (roots.Count == 0)
                    roots = products;

                foreach (var p in roots.OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    var node = new TreeNode(p.Name) { Tag = "P:" + p.Name };
                    AddSpecChildren(node, p.Name, new HashSet<string>());
                    _specTree.Nodes.Add(node);
                }

                _specTree.ExpandAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при обновлении дерева спецификации: " + ex.Message);
            }
        }

        private void AddSpecChildren(TreeNode parent, string componentName, HashSet<string> visited)
        {
            if (visited.Contains(componentName))
                return;

            visited.Add(componentName);

            var specs = _service.GetSpecificationEntries(componentName).ToList();
            foreach (var s in specs)
            {
                var child = new TreeNode($"{s.PartName} (x{s.Quantity})")
                {
                    Tag = "S:" + componentName + "/" + s.PartName
                };
                parent.Nodes.Add(child);

                var partHasSpec = _service.GetAllActiveProducts().Any(p => p.Name == s.PartName && p.SpecPtr != -1);
                if (partHasSpec)
                    AddSpecChildren(child, s.PartName, visited);
            }

            visited.Remove(componentName);
        }

        private void ShowSpecContextMenu(TreeNode node, System.Drawing.Point location)
        {
            var menu = new ContextMenuStrip();
            var add = new ToolStripMenuItem("Добавить", null, (s, e) =>
            {
                _specTree.SelectedNode = node;
                OnAddSpec();
            });

            var del = new ToolStripMenuItem("Удалить", null, (s, e) =>
            {
                _specTree.SelectedNode = node;
                OnDeleteSpec();
            });

            menu.Items.AddRange(new ToolStripItem[] { add, del });
            menu.Show(_specTree, location);
        }

        private string? ResolveComponentNameForSpec()
        {
            if (_specTree.SelectedNode?.Tag is string tag)
            {
                if (tag.StartsWith("P:"))
                    return tag.Substring(2);
                if (tag.StartsWith("S:"))
                {
                    var parts = tag.Substring(2).Split('/');
                    if (parts.Length == 2)
                        return parts[1];
                }
            }

            return null;
        }

        private bool TryDeleteSpecByTag(string tag)
        {
            if (tag.StartsWith("S:"))
            {
                var parts = tag.Substring(2).Split('/');
                if (parts.Length == 2)
                {
                    _service.DeleteFromSpecification(parts[0], parts[1]);
                    return true;
                }
            }

            return false;
        }

        private void SyncEditorWithSelection()
        {
            if (_componentsList.SelectedItems.Count == 0)
                return;

            var name = _componentsList.SelectedItems[0].Text;
            _tbEditName.Text = name;

            var item = _service.GetAllActiveProducts().FirstOrDefault(p => p.Name == name);
            if (!string.IsNullOrEmpty(item.Name))
                _cbEditType.SelectedItem = item.Type;
        }

        private void MarkChanged()
        {
            _hasPendingChanges = true;
        }

        private bool EnsureOpen()
        {
            if (!_service.IsOpen)
            {
                MessageBox.Show("Файл не открыт. Сначала выполните \"Открыть\" или \"Создать\".");
                return false;
            }

            return true;
        }

        private void CreateBackup()
        {
            if (string.IsNullOrEmpty(_service.ProductFilePath) || !File.Exists(_service.ProductFilePath))
                return;

            var dir = Path.GetDirectoryName(_service.ProductFilePath) ?? "";
            var baseName = Path.GetFileNameWithoutExtension(_service.ProductFilePath);

            _backupProductPath = Path.Combine(dir, baseName + ".prd.bak");
            File.Copy(_service.ProductFilePath, _backupProductPath, true);

            var specPath = ResolveSpecPath();
            if (!string.IsNullOrEmpty(specPath) && File.Exists(specPath))
            {
                _backupSpecPath = Path.Combine(dir, Path.GetFileName(specPath) + ".bak");
                File.Copy(specPath, _backupSpecPath, true);
            }
        }

        private void RestoreBackup()
        {
            if (_backupProductPath == null || !File.Exists(_backupProductPath) || _currentProductPath == null)
                return;

            _service.Close();
            File.Copy(_backupProductPath, _currentProductPath, true);

            if (_backupSpecPath != null && File.Exists(_backupSpecPath) && !string.IsNullOrEmpty(_currentSpecPath))
                File.Copy(_backupSpecPath, _currentSpecPath, true);

            _service.Open(_currentProductPath);
        }

        private string? ResolveSpecPath()
        {
            if (string.IsNullOrWhiteSpace(_service.SpecFilePath))
                return null;

            if (Path.IsPathRooted(_service.SpecFilePath))
                return _service.SpecFilePath;

            if (!string.IsNullOrEmpty(_currentProductPath))
            {
                var dir = Path.GetDirectoryName(_currentProductPath) ?? "";
                return Path.Combine(dir, _service.SpecFilePath);
            }

            return _service.SpecFilePath;
        }
    }
}
