using System.Windows;
using Telhai.DotNet.PlayerProject.ViewModels;

namespace Telhai.DotNet.PlayerProject
{
    public partial class EditSongWindow : Window
    {
        public EditSongWindow(EditSongViewModel vm)
        {
            InitializeComponent();

            DataContext = vm;
            vm.RequestClose += () => this.Close();
        }
    }
}
