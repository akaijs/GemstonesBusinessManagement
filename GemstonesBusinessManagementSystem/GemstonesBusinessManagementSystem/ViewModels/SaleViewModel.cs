﻿using GemstonesBusinessManagementSystem.DAL;
using GemstonesBusinessManagementSystem.Models;
using GemstonesBusinessManagementSystem.Resources.Template;
using GemstonesBusinessManagementSystem.Resources.UserControls;
using GemstonesBusinessManagementSystem.Views;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GemstonesBusinessManagementSystem.ViewModels
{
    class SaleViewModel : BaseViewModel
    {
        //Wrap panel
        public ICommand LoadSaleGoodsCommand { get; set; }
        public ICommand SearchCommand { get; set; }
        public ICommand PickGoodsCommand { get; set; }

        //Payment
        public ICommand CompletePaymentCommand { get; set; }
        public ICommand PrintPaymentCommand { get; set; }

        //Customer
        public ICommand SelectCustomerCommand { get; set; }

        //Selected goods
        public ICommand DeleteCommand { get; set; }
        public ICommand ChangeQuantityCommand { get; set; }

        private MainWindow mainWindow;
        private List<Goods> saleGoodsList = GoodsDAL.Instance.GetList();

        private string idCustomer;
        private string customerName;
        private string customerPhoneNumber;
        private string customerClass;
        private string customerAddress;
        private string idBill;
        private string invoiceDate;
        private long quantity = 0;
        private long total = 0;

        public string IdCustomer { get => idCustomer; set { idCustomer = value; OnPropertyChanged(); } }
        public string CustomerName { get => customerName; set { customerName = value; OnPropertyChanged(); } }
        public string CustomerPhoneNumber { get => customerPhoneNumber; set { customerPhoneNumber = value; OnPropertyChanged(); } }
        public string CustomerClass { get => customerClass; set { customerClass = value; OnPropertyChanged(); } }
        public string CustomerAddress { get => customerAddress; set { customerAddress = value; OnPropertyChanged(); } }
        public string IdBill { get => idBill; set { idBill = value; OnPropertyChanged(); } }
        public string InvoiceDate { get => invoiceDate; set { invoiceDate = value; OnPropertyChanged(); } }
        public long Quantity { get => quantity; set { quantity = value; OnPropertyChanged(); } }
        public long Total { get => total; set { total = value; OnPropertyChanged(); } }

        public SaleViewModel()
        {
            LoadSaleGoodsCommand = new RelayCommand<MainWindow>((p) => true, (p) => { LoadDefault(p); LoadSaleGoods(p); });
            PickGoodsCommand = new RelayCommand<SaleGoodsControl>((p) => true, (p) => PickGoods(p));
            SearchCommand = new RelayCommand<MainWindow>((p) => true, (p) => Search(p));

            CompletePaymentCommand = new RelayCommand<MainWindow>((p) => true, (p) => CompletePayment(p));
            PrintPaymentCommand = new RelayCommand<MainWindow>((p) => true, (p) => PrintPayment(p));

            SelectCustomerCommand = new RelayCommand<MainWindow>((p) => true, (p) => SelectCustomer(p));

            DeleteCommand = new RelayCommand<SelectedGoodsControl>((p) => true, (p) => DeleteSelectedGoods(p));
            ChangeQuantityCommand = new RelayCommand<SelectedGoodsControl>((p) => true, (p) => ChangeQuantity(p));
        }
        void OnChangeQuantity()
        {
            Quantity = 0;
            int n = mainWindow.stkSelectedGoods.Children.Count;
            for (int i = 0; i < n; i++)
            {
                SelectedGoodsControl control = (SelectedGoodsControl)mainWindow.stkSelectedGoods.Children[i];
                int tmp = int.Parse(control.nmsQuantity.Text.ToString());
                Quantity += tmp;
            }
        }
        void ChangeQuantity(SelectedGoodsControl control)
        {
            OnChangeQuantity();
            Total -= long.Parse(control.txbTotalPrice.Text);
            control.txbTotalPrice.Text = (int.Parse(control.txbPrice.Text) * control.nmsQuantity.Value).ToString();
            Total += long.Parse(control.txbTotalPrice.Text);
        }
        void DeleteSelectedGoods(SelectedGoodsControl control)
        {
            Total -= int.Parse(control.txbTotalPrice.Text);
            mainWindow.stkSelectedGoods.Children.Remove(control);
            OnChangeQuantity();
        }

        void SelectCustomer(MainWindow window)
        {
            PickCustomerWindow pickCustomerWindow = new PickCustomerWindow();
            pickCustomerWindow.ShowDialog();
            IdCustomer = pickCustomerWindow.txbId.Text;
            CustomerName = pickCustomerWindow.txbName.Text;
            CustomerAddress = pickCustomerWindow.txbAddress.Text;
            CustomerPhoneNumber = pickCustomerWindow.txbPhoneNumber.Text;
            CustomerClass = pickCustomerWindow.txbRank.Text;
        }

        private void CompletePayment(MainWindow window)
        {
            if (string.IsNullOrEmpty(IdCustomer))
            {
                MessageBox.Show("Vui lòng nhập thông tin khách hàng!");
                return;
            }
            if (mainWindow.stkSelectedGoods.Children.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn sản phẩm!");
                return;
            }
            Bill bill = new Bill(ConvertToID(window.txbIdBillSale.Text), CurrentAccount.IdAccount,
                DateTime.Parse(InvoiceDate), Total, ConvertToID(IdCustomer),
                window.txbSaleNote.Text);
            bool isSuccess = BillDAL.Instance.Insert(bill);

            int numOfItems = mainWindow.stkSelectedGoods.Children.Count;
            for (int i = 0; i < numOfItems; i++)
            {
                if (!isSuccess)
                {
                    break;
                }
                SelectedGoodsControl control = (SelectedGoodsControl)mainWindow.stkSelectedGoods.Children[i];

                int quantity = int.Parse(control.nmsQuantity.Text.ToString());
                BillInfo billInfo = new BillInfo(bill.IdBill, ConvertToID(control.txbId.Text), quantity, long.Parse(control.txbPrice.Text));
                BillInfoDAL.Instance.Insert(billInfo);

                isSuccess = GoodsDAL.Instance.UpdateQuantity(ConvertToID(control.txbId.Text), -quantity);
            }

            if (isSuccess)
            {
                var result = MessageBox.Show("Thanh toán thành công! Bạn có muốn in hóa đơn?",
                    "Thông báo", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    PrintPayment(mainWindow);
                }
            }
            else
            {
                MessageBox.Show("Thanh toán thất bại!");
            }
            CustomerDAL.Instance.UpdateTotalSpending(ConvertToID(IdCustomer), Total);
            CustomerViewModel customerVM = (CustomerViewModel)mainWindow.grdCustomer.DataContext;
            customerVM.Load(mainWindow);
            mainWindow.stkSelectedGoods.Children.Clear();
            LoadDefault(mainWindow);
            Search(mainWindow);
        }
        private void PrintPayment(MainWindow window)
        {
            BillTemplate billTemplate = new BillTemplate();

            billTemplate.txbInvoiceDate.Text = InvoiceDate;
            billTemplate.txbIdBill.Text = IdBill;
            billTemplate.txbCustomerName.Text = CustomerName;
            billTemplate.txbCustomerPhoneNumber.Text = CustomerPhoneNumber;
            billTemplate.txbCustomerAddress.Text = CustomerAddress;
            billTemplate.txbTotal.Text = Total.ToString();
            if (string.IsNullOrEmpty(window.txbSaleNote.Text))
            {
                billTemplate.stkNote.Visibility = Visibility.Hidden;
            }
            else
            {
                billTemplate.txbNote.Text = window.txbSaleNote.Text;
            }

            int numOfItems = mainWindow.stkSelectedGoods.Children.Count;
            for (int i = 0; i < numOfItems; i++)
            {
                SelectedGoodsControl selectedControl = (SelectedGoodsControl)mainWindow.stkSelectedGoods.Children[i];
                BillInfoControl billInfoControl = new BillInfoControl();
                billInfoControl.txbOrderNum.Text = (i + 1).ToString();
                billInfoControl.txbName.Text = selectedControl.txbName.Text;
                billInfoControl.txbQuantity.Text = selectedControl.nmsQuantity.Text.ToString();
                billInfoControl.txbUnit.Text = selectedControl.txbUnit.Text;
                billInfoControl.txbUnitPrice.Text = selectedControl.txbPrice.Text;
                billInfoControl.txbTotal.Text = selectedControl.txbTotalPrice.Text;

                billTemplate.stkBillInfo.Children.Add(billInfoControl);
            }
            try
            {
                PrintDialog printDialog = new PrintDialog();
                printDialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA5);

                if (printDialog.ShowDialog() == true)
                {
                    printDialog.PrintVisual(billTemplate.grdPrint, window.txbIdBillSale.Text);
                }
            }
            catch
            {

            }
        }

        public void Search(MainWindow window)
        {
            string namesearching = mainWindow.txtSearchSaleGoods.Text.ToLower();
            saleGoodsList = GoodsDAL.Instance.FindByName(namesearching);
            LoadSaleGoods(mainWindow);
        }
        void PickGoods(SaleGoodsControl control)
        {
            SelectedGoodsControl selectedControl = new SelectedGoodsControl();

            selectedControl.txbId.Text = AddPrefix("SP", int.Parse(control.txbId.Text));
            selectedControl.txbName.Text = control.txbName.Text;
            selectedControl.txbType.Text = control.txbType.Text;
            selectedControl.txbUnit.Text = control.txbUnit.Text;
            selectedControl.txbPrice.Text = control.txbPrice.Text;
            selectedControl.txbTotalPrice.Text =
                (decimal.Parse(control.txbPrice.Text) * selectedControl.nmsQuantity.Value).ToString();
            selectedControl.nmsQuantity.MaxValue = decimal.Parse(control.txbQuantity.Text);

            int numOfItems = mainWindow.stkSelectedGoods.Children.Count;
            for (int i = 0; i < numOfItems; i++)
            {
                SelectedGoodsControl temp = (SelectedGoodsControl)mainWindow.stkSelectedGoods.Children[i];
                if (selectedControl.txbId.Text == temp.txbId.Text)
                {
                    if (int.Parse(control.txbQuantity.Text) == temp.nmsQuantity.Value)
                    {
                        MessageBox.Show("Đã hết hàng!");
                        return;
                    }
                    temp.nmsQuantity.Value++;
                    return;
                }
            }
            Total += int.Parse(selectedControl.txbPrice.Text);
            mainWindow.stkSelectedGoods.Children.Add(selectedControl);
            OnChangeQuantity();
        }
        public void LoadDefault(MainWindow window)
        {
            int maxId = BillDAL.Instance.GetMaxId();
            IdBill = AddPrefix("HD", maxId + 1);
            InvoiceDate = DateTime.Now.ToShortDateString();
            Quantity = 0;
            Total = 0;
            IdCustomer = "";
            CustomerName = "";
            CustomerPhoneNumber = "";
            CustomerClass = "";
            CustomerAddress = "";
        }
        void LoadSaleGoods(MainWindow window)
        {
            mainWindow = window;
            mainWindow.wrpSaleGoods.Children.Clear();

            for (int i = 0; i < saleGoodsList.Count; i++)
            {
                if (saleGoodsList[i].Quantity > 0)
                {
                    SaleGoodsControl control = new SaleGoodsControl();

                    control.txbQuantity.Text = saleGoodsList[i].Quantity.ToString();
                    control.txbId.Text = saleGoodsList[i].IdGoods.ToString();
                    control.imgGood.Source = Converter.Instance.ConvertByteToBitmapImage(saleGoodsList[i].ImageFile);
                    control.txbName.Text = saleGoodsList[i].Name;
                    GoodsType type = GoodsTypeDAL.Instance.GetById(saleGoodsList[i].IdGoodsType);
                    double profitPercentage = type.ProfitPercentage;
                    control.txbPrice.Text = Math.Round(saleGoodsList[i].ImportPrice * (1 + profitPercentage)).ToString();
                    control.txbType.Text = type.Name;
                    control.txbUnit.Text = type.Unit;

                    mainWindow.wrpSaleGoods.Children.Add(control);
                }
            }
        }
    }
}
