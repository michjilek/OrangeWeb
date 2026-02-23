
using System.ComponentModel;

namespace OP_Pages_Library.ViewModel.Interface;
public interface IMV_OP_Template_Page
{
    string Name { get; set; }

    event PropertyChangedEventHandler PropertyChanged;
}
