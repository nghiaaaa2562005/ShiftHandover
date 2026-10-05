using System;
using System.Linq;
using System.Windows;
using ShiftHandOver.Client.Services;
using ShiftHandOver.Share;

namespace ShiftHandOver.Client.Employee
{
    public partial class SelectSalesChannelsDialog : Window
    {
        public ShiftChannelSelection ResultSelection { get; private set; } = new();
        public bool Confirmed { get; private set; } = false;

        private readonly bool _hasPos1 = false;
        private readonly bool _hasPos2 = false;
        private readonly bool _hasPos3 = false;

        private readonly bool _hasBank1 = false;
        private readonly bool _hasBank2 = false;
        private readonly bool _hasBank3 = false;

        public SelectSalesChannelsDialog(
            string branchName,
            string shiftName,
            DateTime workDate,
            ShiftHandoverDetailDTO shiftDetail,
            BranchHandoverConfigDTO? branchConfig,
            ShiftChannelSelection? currentSelection = null)
        {
            InitializeComponent();

            lblBranchShiftInfo.Text = $"Cơ sở: {branchName} | Ca: {shiftName} — Ngày {workDate:dd/MM/yyyy}";

            // 1. Nạp danh sách App POS bán hàng
            if (branchConfig != null && branchConfig.PosConfigs != null && branchConfig.PosConfigs.Any(p => p.IsActive))
            {
                var activePos = branchConfig.PosConfigs.Where(p => p.IsActive).OrderBy(p => p.DisplayOrder).ToList();
                if (activePos.Count > 0)
                {
                    _hasPos1 = true;
                    lblPos1Name.Text = activePos[0].PosName;
                    chkPos1.IsChecked = true;
                    cardPos1.Visibility = Visibility.Visible;
                    imgPos1Logo.Source = ApiService.GetImageSource(activePos[0].ImageUrl);
                    bdrPos1Logo.Visibility = imgPos1Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardPos1.Visibility = Visibility.Collapsed;
                }

                if (activePos.Count > 1)
                {
                    _hasPos2 = true;
                    lblPos2Name.Text = activePos[1].PosName;
                    chkPos2.IsChecked = true;
                    cardPos2.Visibility = Visibility.Visible;
                    imgPos2Logo.Source = ApiService.GetImageSource(activePos[1].ImageUrl);
                    bdrPos2Logo.Visibility = imgPos2Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardPos2.Visibility = Visibility.Collapsed;
                }

                if (activePos.Count > 2)
                {
                    _hasPos3 = true;
                    lblPos3Name.Text = activePos[2].PosName;
                    chkPos3.IsChecked = true;
                    cardPos3.Visibility = Visibility.Visible;
                    imgPos3Logo.Source = ApiService.GetImageSource(activePos[2].ImageUrl);
                    bdrPos3Logo.Visibility = imgPos3Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardPos3.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Fallback từ shiftDetail
                if (!string.IsNullOrWhiteSpace(shiftDetail.Pos1Name) && shiftDetail.Pos1IsActive)
                {
                    _hasPos1 = true;
                    lblPos1Name.Text = shiftDetail.Pos1Name;
                    chkPos1.IsChecked = true;
                    cardPos1.Visibility = Visibility.Visible;
                    imgPos1Logo.Source = ApiService.GetImageSource(shiftDetail.Pos1ImageUrl);
                    bdrPos1Logo.Visibility = imgPos1Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardPos1.Visibility = Visibility.Collapsed;
                }

                if (!string.IsNullOrWhiteSpace(shiftDetail.Pos2Name) && shiftDetail.Pos2IsActive)
                {
                    _hasPos2 = true;
                    lblPos2Name.Text = shiftDetail.Pos2Name;
                    chkPos2.IsChecked = true;
                    cardPos2.Visibility = Visibility.Visible;
                    imgPos2Logo.Source = ApiService.GetImageSource(shiftDetail.Pos2ImageUrl);
                    bdrPos2Logo.Visibility = imgPos2Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardPos2.Visibility = Visibility.Collapsed;
                }

                if (!string.IsNullOrWhiteSpace(shiftDetail.Pos3Name) && shiftDetail.Pos3IsActive)
                {
                    _hasPos3 = true;
                    lblPos3Name.Text = shiftDetail.Pos3Name;
                    chkPos3.IsChecked = true;
                    cardPos3.Visibility = Visibility.Visible;
                    imgPos3Logo.Source = ApiService.GetImageSource(shiftDetail.Pos3ImageUrl);
                    bdrPos3Logo.Visibility = imgPos3Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardPos3.Visibility = Visibility.Collapsed;
                }
            }

            // 2. Nạp danh sách Ngân hàng do Admin đã cấu hình cho cơ sở
            if (branchConfig != null && branchConfig.Banks != null && branchConfig.Banks.Any(b => b.IsActive))
            {
                var activeBanks = branchConfig.Banks.Where(b => b.IsActive).OrderBy(b => b.SlotIndex).ToList();
                if (activeBanks.Count > 0)
                {
                    _hasBank1 = true;
                    lblBank1Name.Text = activeBanks[0].BankName;
                    chkBank1.IsChecked = true;
                    cardBank1.Visibility = Visibility.Visible;
                    imgBank1Logo.Source = ApiService.GetImageSource(activeBanks[0].ImageUrl);
                    bdrBank1Logo.Visibility = imgBank1Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardBank1.Visibility = Visibility.Collapsed;
                }

                if (activeBanks.Count > 1)
                {
                    _hasBank2 = true;
                    lblBank2Name.Text = activeBanks[1].BankName;
                    chkBank2.IsChecked = true;
                    cardBank2.Visibility = Visibility.Visible;
                    imgBank2Logo.Source = ApiService.GetImageSource(activeBanks[1].ImageUrl);
                    bdrBank2Logo.Visibility = imgBank2Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardBank2.Visibility = Visibility.Collapsed;
                }

                if (activeBanks.Count > 2)
                {
                    _hasBank3 = true;
                    lblBank3Name.Text = activeBanks[2].BankName;
                    chkBank3.IsChecked = true;
                    cardBank3.Visibility = Visibility.Visible;
                    imgBank3Logo.Source = ApiService.GetImageSource(activeBanks[2].ImageUrl);
                    bdrBank3Logo.Visibility = imgBank3Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardBank3.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Fallback từ shiftDetail
                if (!string.IsNullOrWhiteSpace(shiftDetail.Bank1Name) && shiftDetail.Bank1IsActive)
                {
                    _hasBank1 = true;
                    lblBank1Name.Text = shiftDetail.Bank1Name;
                    chkBank1.IsChecked = true;
                    cardBank1.Visibility = Visibility.Visible;
                    imgBank1Logo.Source = ApiService.GetImageSource(shiftDetail.Bank1ImageUrl);
                    bdrBank1Logo.Visibility = imgBank1Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardBank1.Visibility = Visibility.Collapsed;
                }

                if (!string.IsNullOrWhiteSpace(shiftDetail.Bank2Name) && shiftDetail.Bank2IsActive)
                {
                    _hasBank2 = true;
                    lblBank2Name.Text = shiftDetail.Bank2Name;
                    chkBank2.IsChecked = true;
                    cardBank2.Visibility = Visibility.Visible;
                    imgBank2Logo.Source = ApiService.GetImageSource(shiftDetail.Bank2ImageUrl);
                    bdrBank2Logo.Visibility = imgBank2Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardBank2.Visibility = Visibility.Collapsed;
                }

                if (!string.IsNullOrWhiteSpace(shiftDetail.Bank3Name) && shiftDetail.Bank3IsActive)
                {
                    _hasBank3 = true;
                    lblBank3Name.Text = shiftDetail.Bank3Name;
                    chkBank3.IsChecked = true;
                    cardBank3.Visibility = Visibility.Visible;
                    imgBank3Logo.Source = ApiService.GetImageSource(shiftDetail.Bank3ImageUrl);
                    bdrBank3Logo.Visibility = imgBank3Logo.Source != null ? Visibility.Visible : Visibility.Collapsed;
                }
                else
                {
                    cardBank3.Visibility = Visibility.Collapsed;
                }
            }

            // Nếu mở lại giữa ca: áp dụng trạng thái tick hiện tại của ca
            if (currentSelection != null)
            {
                if (_hasPos1) chkPos1.IsChecked = currentSelection.Pos1Active;
                if (_hasPos2) chkPos2.IsChecked = currentSelection.Pos2Active;
                if (_hasPos3) chkPos3.IsChecked = currentSelection.Pos3Active;

                if (_hasBank1) chkBank1.IsChecked = currentSelection.Bank1Active;
                if (_hasBank2) chkBank2.IsChecked = currentSelection.Bank2Active;
                if (_hasBank3) chkBank3.IsChecked = currentSelection.Bank3Active;
            }

            // Cho phép click vào cả khung thẻ card để bật/tắt checkbox
            cardPos1.Cursor = System.Windows.Input.Cursors.Hand;
            cardPos2.Cursor = System.Windows.Input.Cursors.Hand;
            cardPos3.Cursor = System.Windows.Input.Cursors.Hand;
            cardBank1.Cursor = System.Windows.Input.Cursors.Hand;
            cardBank2.Cursor = System.Windows.Input.Cursors.Hand;
            cardBank3.Cursor = System.Windows.Input.Cursors.Hand;

            cardPos1.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource != chkPos1) chkPos1.IsChecked = !chkPos1.IsChecked; };
            cardPos2.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource != chkPos2) chkPos2.IsChecked = !chkPos2.IsChecked; };
            cardPos3.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource != chkPos3) chkPos3.IsChecked = !chkPos3.IsChecked; };
            cardBank1.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource != chkBank1) chkBank1.IsChecked = !chkBank1.IsChecked; };
            cardBank2.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource != chkBank2) chkBank2.IsChecked = !chkBank2.IsChecked; };
            cardBank3.MouseLeftButtonUp += (s, e) => { if (e.OriginalSource != chkBank3) chkBank3.IsChecked = !chkBank3.IsChecked; };
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            bool pos1Sel = _hasPos1 && chkPos1.IsChecked == true;
            bool pos2Sel = _hasPos2 && chkPos2.IsChecked == true;
            bool pos3Sel = _hasPos3 && chkPos3.IsChecked == true;

            bool bank1Sel = _hasBank1 && chkBank1.IsChecked == true;
            bool bank2Sel = _hasBank2 && chkBank2.IsChecked == true;
            bool bank3Sel = _hasBank3 && chkBank3.IsChecked == true;

            bool anyPos = pos1Sel || pos2Sel || pos3Sel;
            bool anyBank = bank1Sel || bank2Sel || bank3Sel;

            bool hasAnyPosConfigured = _hasPos1 || _hasPos2 || _hasPos3;
            bool hasAnyBankConfigured = _hasBank1 || _hasBank2 || _hasBank3;

            bool missingPos = hasAnyPosConfigured && !anyPos;
            bool missingBank = hasAnyBankConfigured && !anyBank;

            if (missingPos && missingBank)
            {
                MessageBox.Show(
                    "Bạn chưa chọn App POS bán hàng và Ngân hàng nào!\n\n" +
                    "Vui lòng chọn ít nhất 1 App POS bán hàng và 1 Ngân hàng / Ví điện tử để vào ca làm việc.",
                    "Yêu cầu chọn kênh bán hàng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (missingPos)
            {
                MessageBox.Show(
                    "Bạn chưa chọn App POS bán hàng nào!\n\n" +
                    "Vui lòng chọn ít nhất 1 App POS bán hàng để vào ca làm việc.",
                    "Yêu cầu chọn App POS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (missingBank)
            {
                MessageBox.Show(
                    "Bạn chưa chọn Ngân hàng / Ví điện tử nào!\n\n" +
                    "Vui lòng chọn ít nhất 1 Ngân hàng / Ví điện tử để vào ca làm việc.",
                    "Yêu cầu chọn Ngân hàng",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ResultSelection = new ShiftChannelSelection
            {
                Pos1Active = pos1Sel,
                Pos2Active = pos2Sel,
                Pos3Active = pos3Sel,
                Bank1Active = bank1Sel,
                Bank2Active = bank2Sel,
                Bank3Active = bank3Sel
            };

            Confirmed = true;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            DialogResult = false;
            Close();
        }
    }
}
