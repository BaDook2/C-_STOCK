using Avalonia.Controls;
namespace Stock.Desktop;
public partial class AccountDialog:Window
{public AccountDialog(){InitializeComponent();Opened+=(_,_)=>{if(DataContext is AccountWorkspace model){model.EditorOpen=true;model.LoadSaved.Execute(null);}};Closed+=(_,_)=>{if(DataContext is AccountWorkspace model){model.EditorOpen=false;model.CancelPending();model.ClearInputs();}};}}
