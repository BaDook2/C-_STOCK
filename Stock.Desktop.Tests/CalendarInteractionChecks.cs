using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

internal static partial class Program
{
    private static void CalendarInteractionChecks()
    {
        // August2026 has six calendar rows. Bounds assertions catch the real
        // clipped Fluent header/weekdays that IsDropDownOpen alone missed.
        var picker=new CalendarDatePicker{Width=126,SelectedDate=new DateTime(2026,8,15),SelectedDateFormat=CalendarDatePickerFormat.Custom,CustomDateFormatString="yyyy-MM-dd"};
        var window=new Window{Width=420,Height=340,Content=new StackPanel{Margin=new Thickness(16),Children={picker}}};window.Show();Dispatcher.UIThread.RunJobs();
        var editor=picker.GetVisualDescendants().OfType<TextBox>().Single();var presenter=editor.GetVisualDescendants().OfType<TextPresenter>().Single();
        Check(presenter.Bounds.Width>=presenter.TextLayout.Width&&presenter.Bounds.Height>=presenter.TextLayout.Height,"compact date input fits its entire year/month/day and text height");
        var open=picker.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="PART_Button");ClickAt(window,open,.5);
        var popup=picker.GetVisualDescendants().OfType<Popup>().Single();var calendar=(Calendar)popup.Child!;var root=(WindowBase)TopLevel.GetTopLevel(calendar)!;
        Check(picker.IsDropDownOpen&&calendar.Bounds.Size==new Size(202,201),"actual date icon opens a complete202x201 calendar popup");
        bool Inside(Control control){var point=control.TranslatePoint(default,calendar)!.Value;return point.X>=0&&point.Y>=0&&point.X+control.Bounds.Width<=calendar.Bounds.Width+.5&&point.Y+control.Bounds.Height<=calendar.Bounds.Height+.5;}
        void Click(Control control){var point=control.TranslatePoint(new(control.Bounds.Width/2,control.Bounds.Height/2),root)!.Value;root.MouseMove(point);root.MouseDown(point,MouseButton.Left);root.MouseUp(point,MouseButton.Left);Dispatcher.UIThread.RunJobs();}
        var days=calendar.GetVisualDescendants().OfType<CalendarDayButton>().ToArray();var header=calendar.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="PART_HeaderButton");
        Check(days.Length==42&&days.All(Inside)&&Inside(header),"six full weeks and the month header remain inside the popup");
        var headerText=header.GetVisualDescendants().OfType<TextBlock>().Single();Check(headerText.TextLayout.Width<header.Bounds.Width,"calendar year/month header is not horizontally clipped");
        var next=calendar.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="PART_NextButton");var previous=calendar.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="PART_PreviousButton");
        Click(next);Check(calendar.DisplayDate.Month==9,"next arrow moves the actual calendar to the following month");Click(previous);Check(calendar.DisplayDate.Month==8,"previous arrow restores the prior month");
        Click(header);var months=calendar.GetVisualDescendants().OfType<CalendarButton>().Where(b=>b.IsEffectivelyVisible).ToArray();Check(calendar.DisplayMode==CalendarMode.Year&&months.Length==12&&months.All(Inside),"year view shows every month without the old290px minimum overflowing");
        Click(header);Check(calendar.DisplayMode==CalendarMode.Decade&&calendar.GetVisualDescendants().OfType<CalendarButton>().Where(b=>b.IsEffectivelyVisible).All(Inside),"decade view and its year buttons also fit the compact layout");
        var year=calendar.GetVisualDescendants().OfType<CalendarButton>().Single(b=>b.IsEffectivelyVisible&&b.Content?.ToString()=="2026");Click(year);Check(calendar.DisplayMode==CalendarMode.Year,"clicking a year returns to its selectable months");
        var august=calendar.GetVisualDescendants().OfType<CalendarButton>().Where(b=>b.IsEffectivelyVisible).ElementAt(7);Click(august);Check(calendar.DisplayMode==CalendarMode.Month&&calendar.DisplayDate.Month==8,"clicking August restores the correct daily calendar");
        var selected=calendar.GetVisualDescendants().OfType<CalendarDayButton>().Single(b=>b.DataContext is DateTime d&&d.Date==new DateTime(2026,8,20));Click(selected);
        Check(picker.SelectedDate==new DateTime(2026,8,20)&&!picker.IsDropDownOpen,"a real date-cell click updates the picker and closes the popup");
        window.Close();
    }
}
