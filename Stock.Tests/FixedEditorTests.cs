using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Stock.App;
using Stock.Core.Research.Oversold;
using Stock.Core.Trading;
internal static partial class Program
{
    private static void FixedEditorChecks()
    {
        RequestedLabelChecks();var kind=ParameterInputKind.Decimal;var list=ParameterInputKind.DecimalList;
        void Typed(string text,int at,int length,string input,string expected,int caret,ParameterInputKind? mode=null)
        {var r=FixedDecimalEdit.Type(text,at,length,input,mode??kind);Check(r.Accepted&&r.Text==expected&&r.Caret==caret,$"overwrite {text} at {at} with {input}: {r.Text} caret {r.Caret}");}
        Typed("123.45",0,0,"9","9,123.45",1);Typed("123.45",3,0,"6","1,236.45",5);Typed("123.45",5,0,"7","123.47",6);
        Typed("1,234.50",1,0,"9","19,234.50",2);Typed("-12.30",0,0,"9","-912.30",2);Typed("123.45",0,3,"9","9.45",1);Typed("123.45",0,6,"8","8.00",1);
        Typed("123.45",0,0,".","123.45",4);Typed("-0.00",1,0,"-","0.00",0);Typed("0.00",0,0,"-","-0.00",1);Typed("",0,0,"-","-",1);Typed("-",1,0,"2","-2.00",2);
        Typed("40.00, 30.00, 30.00",2,0,"9","409.00, 30.00, 30.00",3,list);Typed("40.00, 30.00, 30.00",5,0,"9","40.00, 930.00, 30.00",8,list);
        Typed("12.34",0,0,"9","912.34",1);Typed("12.34",1,0,"9","192.34",2);
        Typed("12.34",2,0,"9","129.34",3);Typed("12.34",3,0,"9","12.94",4);Typed("12.34",4,0,"9","12.39",5);
        Typed("1.00",1,0,"2","12.00",2);Typed("40.00, 30.00",9,0,"2","40.00, 302.00",10,list);
        Typed("40.00, 30.00",10,0,"9","40.00, 30.90",11,list);
        Check(!FixedDecimalEdit.Type("1.00",4,0,"2",kind).Accepted,"fraction end refuses a third digit");
        Check(!FixedDecimalEdit.Type("1.00",0,0,"한",kind).Accepted&&!FixedDecimalEdit.Type("1.00",0,0,"e",kind).Accepted,"IME and exponent text cannot change fixed decimal input");
        Check(FixedDecimalEdit.Display("40,30,30",list)=="40.00, 30.00, 30.00"&&FixedDecimalEdit.Display("120,240,480,999",ParameterInputKind.IntegerList)=="120,240,480,999","list display never becomes a scalar grouped number");
        Check(FixedDecimalEdit.Display("0.015",kind)=="0.02"&&FixedDecimalEdit.Rounded("0.015",kind)&&!FixedDecimalEdit.Rounded("1.00",kind),"precise legacy value is rounded only for display with detectable difference");
        Check(FixedDecimalEdit.Display("-0",kind)=="-0.00","negative zero draft sign survives formatting");
        foreach(var text in new[]{"1.234","1,23.45","NaN","1e3","12한","--1","1.2.3"})Check(!FixedDecimalEdit.Paste("1.00",0,4,text,kind).Accepted,"invalid scalar paste rejected: "+text);
        foreach(var text in new[]{"40.123,30,30","40,,60","40, 30한, 30"})Check(!FixedDecimalEdit.Paste("40.00, 60.00",0,12,text,list).Accepted,"invalid ratio paste rejected: "+text);
        Check(FixedDecimalEdit.Paste("1.00",0,4,"1,234.56",kind).Text=="1,234.56"&&FixedDecimalEdit.Paste("1.00",0,4,"-25",kind).Text=="-25.00","selection paste changes integer width and accepts negatives");
        Check(FixedDecimalEdit.Paste("1.00",0,0,"2",kind).Text=="21.00","explicit paste can grow integer part");
        Check(FixedDecimalEdit.Paste("40.00, 30.00, 30.00",0,19,"50,50",list).Text=="50.00, 50.00","full-list paste changes ratio count without merging components");
        Check(FixedDecimalEdit.Delete("123.45",5,0,false,kind).Text=="123.40"&&FixedDecimalEdit.Delete("123.45",5,0,true,kind).Text=="123.05","fraction delete and backspace clear a digit while retaining two places");
        Check(FixedDecimalEdit.Delete("1.00",0,0,false,kind).Text=="0.00"&&FixedDecimalEdit.Delete("1,234.50",0,0,false,kind).Text=="234.50","integer deletion keeps valid decimal structure and regrouping");
        Check(FixedDecimalEdit.Delete("12.34",3,0,true,kind).Text=="1.34","backspace skips decimal point instead of deleting it");
        Check(FixedDecimalEdit.Delete("40.00, 60.00",5,0,false,list) is {Text:"40.00, 60.00",Commit:false},"unselected ratio delimiter cannot be deleted");
        Check(FixedDecimalEdit.Delete("1.00",0,4,false,kind).Text=="","select-all delete creates empty invalid draft instead of silently saving zero");
    }
    private static void FixedEditorUiTests(MainWindow window,string output)
    {
        FixedEditorChecks();using var db=new TradingStore(Path.Combine(Dir,"fixed-editor.db"));var config=DailyConfig();config.CommissionPercent=.015m;config.MovingAveragePeriods="1";config.MinimumSupportCount=1;config.UsePivotSupport=config.UseApproximateVolumeZone=false;
        db.State("oversold-config-v1",config);db.State("oversold-facts-v1",DailyFacts(5));using var vm=new WorkspaceViewModel(_=>false,provider:new DailyFixture(),store:db);
        var previous=window.DataContext;window.DataContext=vm;vm.ActiveTab=2;
        void Layout(){window.UpdateLayout();Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);}
        for(var pass=0;pass<4;pass++){foreach(var e in Descendants(window).OfType<Expander>().ToArray())e.IsExpanded=true;Layout();}
        ParameterField Field(string key)=>vm.OversoldGroups.SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key==key);
        TextBox Box(string key)=>Descendants(window).OfType<TextBox>().Single(t=>t.DataContext==Field(key));
        void Do(string key){vm.Actions[key].Execute(null);PumpUntil(()=>!vm.Busy);Layout();}
        void Type(TextBox box,string text){box.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,box,text)){RoutedEvent=TextCompositionManager.PreviewTextInputEvent});Layout();}
        void Paste(TextBox box,string text){var data=new DataObject(DataFormats.UnicodeText,text);var e=new DataObjectPastingEventArgs(data,false,DataFormats.UnicodeText){RoutedEvent=DataObject.PastingEvent};box.RaiseEvent(e);Layout();}
        void Key(TextBox box,System.Windows.Input.Key key){var e=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(window),0,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};box.RaiseEvent(e);if(!e.Handled){e.RoutedEvent=Keyboard.KeyDownEvent;box.RaiseEvent(e);}Layout();}
        var initial=Serialization.Pack(db.State<OversoldConfig>("oversold-config-v1"));var fee=Box("CommissionPercent");fee.Focus();Keyboard.Focus(fee);Layout();Check(fee.Text=="0.02"&&Field("CommissionPercent").Value=="0.015"&&fee.ToolTip.ToString()!.Contains("0.015"),"WPF fixed input focus preserves exact legacy source and explains rounding");Keyboard.ClearFocus();Layout();Do("낙주설정저장");Check(fee.Text=="0.02"&&Serialization.Pack(db.State<OversoldConfig>("oversold-config-v1"))==initial,"focus blur and save without edits preserve full config and .015 fee");
        var ratio=Box("EntryRatios");ratio.Focus();Keyboard.Focus(ratio);Layout();Check(ratio.Text=="40.00, 30.00, 30.00","ratio focus preserves three independent values");Keyboard.ClearFocus();Layout();Check(ratio.Text=="40.00, 30.00, 30.00"&&!NumericDisplay.GetPercent(ratio),"ratio blur never enables scalar percentage overlay");
        Field("MovingAveragePeriods").Value="120,240,480,999";Layout();var ma=Box("MovingAveragePeriods");ma.Focus();Keyboard.ClearFocus();Layout();Check(ma.Text=="120,240,480,999","MA periods remain an integer list through focus changes");Field("MovingAveragePeriods").Value="1";
        var period=Box("RsiPeriod");period.SetCurrentValue(TextBox.TextProperty,"1000");period.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,period,fee){RoutedEvent=Keyboard.LostKeyboardFocusEvent});Layout();Check(period.Text=="1,000"&&!period.Text.Contains('.')&&Field("RsiPeriod").Value=="1,000","integer periods retain thousands grouping without decimal places");Field("RsiPeriod").Value="14";
        fee.Select(3,0);Type(fee,"5");Check(fee.Text=="0.05"&&fee.CaretIndex==4&&Field("CommissionPercent").Value=="0.05","actual text-input event overwrites fractional digit and explicitly commits two-place value");
        fee.SelectAll();Paste(fee,"0.015");Check(fee.Text=="0.05"&&FixedDecimalInput.GetError(fee).Contains("거부"),"actual paste event rejects extra precision with visible reason");
        fee.SelectAll();Paste(fee,"0.25");Do("낙주설정저장");Check(db.State<OversoldConfig>("oversold-config-v1")!.CommissionPercent==.25m,"only explicitly edited precision is persisted");
        var money=Box("InitialCapital");money.SelectAll();Paste(money,"12,345.67");money.Select(2,0);Type(money,"9");Check(money.Text=="129,345.67"&&money.CaretIndex==3,"WPF integer insertion regroups thousands and advances caret");money.SelectAll();Paste(money,"12,945.67");money.Select(7,0);Type(money,"8");Check(money.Text=="12,945.87"&&money.CaretIndex==8,"WPF overwrite strictly after decimal point advances into fraction");Type(money,"9");var before=money.Text;Type(money,"1");Check(money.Text==before&&money.CaretIndex==money.Text.Length,"WPF third fractional digit is rejected");
        var originalMoney=money.Text;money.SelectAll();Type(money,"1");Type(money,"2");Type(money,"3");Check(money.Text=="123.00"&&money.CaretIndex==3,"uninterrupted integer typing grows width at decimal boundary");
        money.Select(3,0);Type(money,"4");Check(money.Text=="1,234.00"&&money.CaretIndex==5,"explicit caret placement immediately before dot inserts integer");
        Type(money,".");Type(money,"5");Type(money,"6");Check(money.Text=="1,234.56"&&money.CaretIndex==8,"explicit dot key enters two-digit overwrite zone");money.SelectAll();Paste(money,originalMoney);
        ratio.Select(2,0);Type(ratio,"2");Check(ratio.Text=="402.00, 30.00, 30.00"&&ratio.CaretIndex==3,"ratio element integer boundary inserts without merging list");Type(ratio,".");Type(ratio,"5");Check(ratio.Text=="402.50, 30.00, 30.00","ratio fraction overwrites only after decimal");ratio.SelectAll();Paste(ratio,"40,30,30");
        money.Select(0,0);Type(money,".");Check(money.CaretIndex==money.Text.IndexOf('.')+1,"decimal-point key navigates without creating duplicate point");
        money.Select(0,2);Type(money,"7");Check(money.Text=="7,945.89","WPF selection replacement normalizes grouping and preserves fraction");
        money.Select(money.Text.Length,0);Key(money,System.Windows.Input.Key.Back);Check(money.Text.EndsWith(".80"),"WPF backspace clears fractional digit");money.Select(0,0);Key(money,System.Windows.Input.Key.Delete);Check(money.Text=="945.80","WPF Delete removes integer digit without removing decimal");
        money.Focus();money.Select(3,0);Key(money,System.Windows.Input.Key.Home);Check(money.CaretIndex==0,"native Home navigation retained");Key(money,System.Windows.Input.Key.End);Check(money.CaretIndex==money.Text.Length,"native End navigation retained");Key(money,System.Windows.Input.Key.Left);Check(money.CaretIndex==money.Text.Length-1,"native arrow navigation retained");
        money.SelectAll();Key(money,System.Windows.Input.Key.Delete);Do("낙주설정저장");Check(money.Text==""&&Field("InitialCapital").Error!=""&&db.State<OversoldConfig>("oversold-config-v1")!.InitialCapital==10000,"empty draft is invalid and leaves saved value intact");money.SelectAll();Paste(money,"12,000");
        var negative=Box("MinimumDrawdownPercent");negative.SelectAll();Paste(negative,"-20");negative.Select(0,0);Type(negative,"9");Check(negative.Text=="-920.00"&&negative.CaretIndex==2,"negative sign protected during integer insertion");negative.SelectAll();Paste(negative,"-0");Check(negative.Text=="-0.00"&&Field("MinimumDrawdownPercent").Error!="","negative zero remains visible with domain validation");negative.SelectAll();Paste(negative,"-20");
        before=negative.Text;Type(negative,"한");Check(negative.Text==before&&!InputMethod.GetIsInputMethodEnabled(negative)&&FixedDecimalInput.GetError(negative)!="","non-numeric composition rejected and IME disabled only for decimal editor");
        Field("MaxEntries").Value="2";ratio.SelectAll();Paste(ratio,"50,50");vm.Preset="두 회차";Do("추가");Check(db.State<OversoldConfig>("oversold-preset:두 회차") is {MaxEntries:2,EntryRatios:"50.00, 50.00"},"variable-length ratio list persists as separate values with matching entry count");
        using(var restored=new WorkspaceViewModel(_=>false,provider:new DailyFixture(),store:db))Check(restored.OversoldGroups.SelectMany(g=>g.Fields).Single(f=>f.Metadata.Key=="EntryRatios").InputText=="50.00, 50.00","ratio list retains independent two-place display after reopening");
        ratio.SelectAll();Paste(ratio,"40,40");Do("낙주설정저장");Check(vm.Status.Contains("100")&&db.State<OversoldConfig>("oversold-config-v1")!.EntryRatios=="50.00, 50.00","ratio sum validation remains active after fixed-list editing");ratio.SelectAll();Paste(ratio,"50,50");Field("MaxEntries").Value="3";Do("낙주설정저장");Check(vm.Status.Contains("횟수"),"ratio count cross validation remains active");Field("MaxEntries").Value="2";Do("저장");
        var fields=vm.OversoldGroups.SelectMany(g=>g.Fields).ToArray();var rows=vm.OversoldGroups.SelectMany(g=>g.Rows!).ToArray();Check(fields.Length==59&&rows.Length==26&&rows.All(r=>r.Cells.Count==3)&&rows.SelectMany(r=>r.Cells).Count(c=>c.IsEmpty)==19&&rows.SelectMany(r=>r.Cells).Where(c=>!c.IsEmpty).Select(c=>c.Field!.Metadata.Key).Distinct().Count()==59,"26 explicit three-cell rows keep all59 fields and19 intentional blanks");
        Check(rows.Any(r=>r.Cells.Select(c=>c.Field?.Metadata.Key).SequenceEqual(new[]{"UseRsiFilter","RsiPeriod","MaximumRsi"}))&&rows.Any(r=>r.Cells.Select(c=>c.Field?.Metadata.Key).SequenceEqual(new[]{"SupportBreakPercent",null,null})),"semantic RSI row and isolated support-stop row are explicit");
        var width=window.Width;var height=window.Height;
        foreach(var size in new[]{(1540d,1040d),(1160d,760d)})
        {
            window.Width=size.Item1;window.Height=size.Item2;Layout();var panel=Descendants(window).OfType<OversoldParameterPanel>().Single();var inputs=Descendants(panel).OfType<TextBox>().Where(b=>b.DataContext is ParameterField f&&f.IsText&&b.IsVisible).ToArray();
            Check(inputs.All(b=>Math.Abs(b.ActualHeight-32)<1&&Math.Abs(b.ActualWidth-180)<1&&b.Padding==new Thickness(8,4,8,4)),"all strategy numeric/list boxes have same compact size at "+size.Item1);
            var rowViews=Descendants(panel).OfType<ItemsControl>().Where(c=>c.DataContext is ParameterRow&&ReferenceEquals(c.ItemsSource,((ParameterRow)c.DataContext).Cells)).ToArray();
            Check(rowViews.Length==26&&rowViews.All(r=>{var p=r.TransformToAncestor(panel).Transform(new Point());return p.X>=0&&p.X+r.ActualWidth<=panel.ActualWidth+1;}),"semantic rows fit panel without horizontal clipping at "+size.Item1);
            var blank=Descendants(panel).OfType<Border>().Where(b=>b.DataContext is ParameterCell {IsEmpty:true}).ToArray();Check(blank.Length==19&&blank.All(b=>b.Visibility==Visibility.Hidden&&b.ActualWidth>0),"blank cells reserve space instead of repacking unrelated fields at "+size.Item1);
            Descendants(panel).OfType<Border>().Single(b=>b.MinHeight==145&&b.DataContext==Field("RsiPeriod")).BringIntoView();Render((FrameworkElement)window.Content,Path.Combine(output,$"fixed-editor-filter-{size.Item1}.png"));Descendants(panel).OfType<Border>().Single(b=>b.MinHeight==145&&b.DataContext==Field("EntryRatios")).BringIntoView();Render((FrameworkElement)window.Content,Path.Combine(output,$"fixed-editor-ratios-{size.Item1}.png"));
        }
        window.DataContext=previous;window.Width=width;window.Height=height;
    }
}
