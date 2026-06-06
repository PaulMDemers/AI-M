using AIM.Core.PendingActions;

namespace AIM.Desktop.WinForms;

internal sealed class PendingActionsReviewForm : Form
{
    private readonly IPendingAgentActionQueue _pendingAgentActionQueue;
    private readonly FlowLayoutPanel _list = new();
    private readonly Label _header = ClassicAim.Label("No pending AI actions", ClassicAim.BoldFont, ClassicAim.AimBlue);

    public PendingActionsReviewForm(IPendingAgentActionQueue pendingAgentActionQueue)
    {
        _pendingAgentActionQueue = pendingAgentActionQueue;
        ClassicAim.ApplyClassicForm(this);
        Text = "Pending AI Actions";
        Width = 600;
        Height = 430;
        MinimumSize = new Size(480, 340);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        BuildUi();
        _pendingAgentActionQueue.ActionsChanged += OnActionsChanged;
        FormClosed += (_, _) => _pendingAgentActionQueue.ActionsChanged -= OnActionsChanged;
        RefreshList();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var headerPanel = new Panel
        {
            Height = 44,
            Dock = DockStyle.Fill,
            BackColor = ClassicAim.AwayYellow,
            BorderStyle = BorderStyle.Fixed3D,
            Padding = new Padding(8, 6, 8, 6)
        };
        _header.Location = new Point(8, 7);
        headerPanel.Controls.Add(_header);

        _list.Dock = DockStyle.Fill;
        _list.AutoScroll = true;
        _list.FlowDirection = FlowDirection.TopDown;
        _list.WrapContents = false;
        _list.BackColor = SystemColors.Window;
        _list.BorderStyle = BorderStyle.Fixed3D;

        var close = ClassicAim.Button("Close");
        close.Width = 86;
        close.Anchor = AnchorStyles.Right;
        close.Click += (_, _) => Close();

        root.Controls.Add(headerPanel, 0, 0);
        root.Controls.Add(_list, 0, 1);
        root.Controls.Add(close, 0, 2);
        Controls.Add(root);
    }

    private void OnActionsChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(RefreshList));
            return;
        }

        RefreshList();
    }

    private void RefreshList()
    {
        _header.Text = _pendingAgentActionQueue.Actions.Count == 1
            ? "1 pending AI action"
            : $"{_pendingAgentActionQueue.Actions.Count} pending AI actions";
        _list.Controls.Clear();

        if (_pendingAgentActionQueue.Actions.Count == 0)
        {
            var empty = ClassicAim.Label("No pending actions.", ClassicAim.UiFont, SystemColors.GrayText);
            empty.Margin = new Padding(10);
            _list.Controls.Add(empty);
            return;
        }

        foreach (var action in _pendingAgentActionQueue.Actions.ToArray())
        {
            _list.Controls.Add(CreateActionPanel(action));
        }
    }

    private Control CreateActionPanel(PendingAgentAction action)
    {
        var panel = new Panel
        {
            Width = Math.Max(420, ClientSize.Width - 40),
            Height = 104,
            BorderStyle = BorderStyle.Fixed3D,
            BackColor = Color.FromArgb(255, 250, 214),
            Margin = new Padding(6)
        };
        var text = new Label
        {
            Text = $"{action.Title}\r\n{action.SourceLabel}\r\n{action.Detail}\r\n{action.ApprovalNote}",
            Font = ClassicAim.SmallFont,
            Location = new Point(8, 7),
            Size = new Size(panel.Width - 180, 88),
            AutoEllipsis = true
        };
        var approve = ClassicAim.Button("Approve");
        approve.Size = new Size(76, 24);
        approve.Location = new Point(panel.Width - 164, 38);
        approve.Enabled = action.CanApprove;
        approve.Click += async (_, _) =>
        {
            approve.Enabled = false;
            await _pendingAgentActionQueue.ApproveAsync(action);
            RefreshList();
        };

        var deny = ClassicAim.Button("Deny");
        deny.Size = new Size(62, 24);
        deny.Location = new Point(panel.Width - 82, 38);
        deny.Click += (_, _) =>
        {
            _pendingAgentActionQueue.Remove(action.Id);
            RefreshList();
        };

        panel.Controls.AddRange([text, approve, deny]);
        return panel;
    }
}
