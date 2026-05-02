using System.Diagnostics;
using System.Text;
using System.Drawing.Drawing2D;
using Microsoft.Data.Sqlite;

namespace AION.WinForms;

public sealed partial class MainForm : Form
{
    private static TargetCommandInput? PromptTargetCommand()
    {
        using var dialog = new Form
        {
            Text = "추가/삭제",
            Width = 430,
            Height = 220,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var titleLabel = new Label
        {
            Left = 20,
            Top = 20,
            Width = 380,
            Height = 20,
            Text = "닉네임 입력",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(64, 64, 64)
        };
        var nicknameTextBox = new TextBox
        {
            Left = 20,
            Top = 50,
            Width = 380,
            Height = 30,
            Font = new Font("Segoe UI", 11, FontStyle.Regular)
        };

        var buttonPanel = new Panel
        {
            Left = 20,
            Top = 112,
            Width = 380,
            Height = 44
        };

        var addButton = new Button { Text = "추가", Left = 68, Top = 6, Width = 78, Height = 30 };
        var removeButton = new Button { Text = "삭제", Left = 151, Top = 6, Width = 78, Height = 30 };
        var cancelButton = new Button { Text = "취소", Left = 234, Top = 6, Width = 78, Height = 30 };

        addButton.UseVisualStyleBackColor = true;
        removeButton.UseVisualStyleBackColor = true;
        cancelButton.UseVisualStyleBackColor = true;

        TargetCommandInput? selected = null;
        addButton.Click += (_, _) =>
        {
            string nickname = nicknameTextBox.Text.Trim();
            if (nickname.Length == 0)
            {
                MessageBox.Show("닉네임을 입력하세요.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nicknameTextBox.Focus();
                return;
            }

            selected = new TargetCommandInput(TargetAction.Add, nickname);
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        removeButton.Click += (_, _) =>
        {
            string nickname = nicknameTextBox.Text.Trim();
            if (nickname.Length == 0)
            {
                MessageBox.Show("닉네임을 입력하세요.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                nicknameTextBox.Focus();
                return;
            }

            selected = new TargetCommandInput(TargetAction.Remove, nickname);
            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        };
        cancelButton.Click += (_, _) =>
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        };

        buttonPanel.Controls.Add(addButton);
        buttonPanel.Controls.Add(removeButton);
        buttonPanel.Controls.Add(cancelButton);

        dialog.Controls.Add(titleLabel);
        dialog.Controls.Add(nicknameTextBox);
        dialog.Controls.Add(buttonPanel);
        dialog.AcceptButton = addButton;
        dialog.CancelButton = cancelButton;
        dialog.Shown += (_, _) =>
        {
            ApplyRoundedRegion(nicknameTextBox, 8);
            nicknameTextBox.Focus();
        };

        return dialog.ShowDialog() == DialogResult.OK ? selected : null;
    }
    private static void ApplyRoundedRegion(Control control, int radius)
    {
        int diameter = radius * 2;
        var rect = new Rectangle(0, 0, control.Width, control.Height);
        using var path = new GraphicsPath();
        path.StartFigure();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        control.Region = new Region(path);
    }
    private enum TargetAction
    {
        Add,
        Remove
    }

    private sealed record TargetCommandInput(TargetAction Action, string Nickname);
}

