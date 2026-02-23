using OP_Pages_Library.ViewModel.Interface;

namespace OP_Pages_Library.ViewModel;
public class MV_OP_Template_Page : MVBase, IMV_OP_Template_Page
{
    #region Private Properties
    private string _name = "Name";
    #endregion

    public MV_OP_Template_Page()
    {
    }

    #region Public Properties
    public string Name
    {
        get { return _name; }
        set { _name = value; RaisePropertyChanged(nameof(Name)); }
    }
    #endregion
}
