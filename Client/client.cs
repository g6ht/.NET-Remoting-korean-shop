using RemoteObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Activation;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Http;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Serialization.Formatters;
using System.Threading;

namespace Client
{
    internal class client
    {
        static void Main(string[] args)
        {
            // Способ 1: Программная конфигурация
            ConfigureRemotingProgrammatically();

            // Способ 2: Конфигурация через файл
            //RemotingConfiguration.Configure("Client.exe.config", false);

            // Создание объекта через TCP
            object[] tcpAttr = { new UrlAttribute("tcp://172.20.10.13:8086/") }; // ПОМЕНЯТЬ IP
            User tcpUser = (User)Activator.CreateInstance(typeof(User), null, tcpAttr);

            // Создание объекта через HTTP
            object[] httpAttr = { new UrlAttribute("http://172.20.10.13:8087/") };

            if (tcpUser == null)
            {
                Console.WriteLine("Ошибка: сервер недоступен");
                return;
            }

            // Настройка спонсора для TCP соединения
            ILease tcpLease = (ILease)tcpUser.GetLifetimeService();
            MySponsor tcpSponsor = new MySponsor("tcpUser");
            tcpLease.Register(tcpSponsor);

            // authorization
            while (true)
            {
                Console.WriteLine("┌───────────────────────────┐");
                Console.WriteLine("│ 1 - Войти в аккаунт       │");
                Console.WriteLine("│ 2 - Зарегистрироваться    │");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("│ 0 - Выход                 │");
                Console.ResetColor();
                Console.WriteLine("└───────────────────────────┘");
                string answer = Console.ReadLine();
                if (answer == "0")
                {
                    return;
                }
                else if (answer == "1")
                {
                    Console.WriteLine("Введите почту: ");
                    string email = Console.ReadLine();
                    Console.WriteLine("Введите пароль: ");
                    string password = ReadPasswordHidden();
                    string result = tcpUser.Authenticate(email, password);
                    Console.WriteLine(result);
                    if (result == "Вход успешный") {
                        break;
                    }
                }
                else if (answer == "2")
                {
                    Console.WriteLine("Введите почту: ");
                    string email = Console.ReadLine();
                    Console.WriteLine("Введите пароль: ");
                    string password = ReadPasswordHidden();
                    Console.WriteLine("Повторите пароль: ");
                    string password2 = ReadPasswordHidden();
                    Console.WriteLine("Введите имя: ");
                    string name = Console.ReadLine();
                    string result = tcpUser.CreateUser(email, password, password2, name);
                    Console.WriteLine(result);
                    if (result == "Пользователь создан")
                    {
                        break;
                    }
                }
                else
                {
                    Console.WriteLine("Неверная команда");
                }
            }

            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"\nДОБРО ПОЖАЛОВАТЬ, {tcpUser.GetName().ToUpper()}\n");
            Console.ResetColor();

            // for admin
            if (tcpUser.userIsAdmin())
            {
                GoodsAdm goodsAdm = (GoodsAdm)Activator.CreateInstance(typeof(GoodsAdm), null, httpAttr);
                ILease leaseAdm = (ILease)goodsAdm.GetLifetimeService();
                MySponsor sponsorGoodsAdm = new MySponsor("goodsAdmhttp");
                
                leaseAdm.Register(sponsorGoodsAdm);
                while (true)
                {
                    Console.WriteLine("┌─────────────────────────────────┐");
                    Console.WriteLine("│ 1 - Просмотреть все товары      │");
                    Console.WriteLine("│ 2 - Просмотреть все категории   │");
                    Console.WriteLine("│ 3 - Найти товары по категории   │");
                    Console.WriteLine("│ 4 - История заказов             │");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("│ 0 - Выход                       │");
                    Console.ResetColor();
                    Console.WriteLine("└─────────────────────────────────┘");
                    string answer = Console.ReadLine();
                    if (answer == "0")
                    {
                        return;
                    }
                    else if (answer == "1")
                    {
                        Console.WriteLine(goodsAdm.GetAllGoods());
                        Console.WriteLine("Чтобы отредактировать товар, введите его идентификатор, чтобы добавить новый товар, введите \"new\" (0 для возврата)");
                        string good_id = Console.ReadLine();
                        if (good_id == "new")
                        {
                            Console.WriteLine("Введите название:");
                            string title = Console.ReadLine();
                            Console.WriteLine("Введите описание:");
                            string description = Console.ReadLine();
                            Console.WriteLine("Введите количество:");
                            string amount = Console.ReadLine();
                            Console.WriteLine("Введите цену:");
                            string price = Console.ReadLine();
                            Console.WriteLine("Введите название категории:");
                            string category = Console.ReadLine();
                            Console.WriteLine(goodsAdm.AddNewGood(title, description, amount, price, category));
                        }
                        else if (good_id != "0")
                        {
                            ChangeGood(good_id, goodsAdm);
                        }
                    }
                    else if (answer == "2")
                    {
                        Console.WriteLine(goodsAdm.GetAllCategoriesForAdmin());
                        Console.WriteLine("Чтобы отредактировать категорию, введите ее идентификатор, чтобы добавить новую категорию, введите \"new\" (0 для возврата).");
                        string cat_id = Console.ReadLine();
                        if (cat_id == "new")
                        {
                            Console.WriteLine("Введите название категории:");
                            string title = Console.ReadLine();
                            Console.WriteLine(goodsAdm.AddNewCategory(title));
                        }
                        else if (cat_id != "0")
                        {
                            Console.WriteLine("Введите новое название категории:");
                            string newTitle = Console.ReadLine();
                            Console.WriteLine(goodsAdm.EditCategory(cat_id, newTitle));
                        }
                    }
                    else if (answer == "3")
                    {
                        Console.WriteLine("Категории:");
                        Console.WriteLine(goodsAdm.GetAllCategoriesForAdmin());
                        Console.WriteLine("Введите название желаемой категории:");
                        string category = Console.ReadLine();
                        Console.WriteLine(goodsAdm.GetGoodsByCategory(category));
                        Console.WriteLine("Чтобы отредактировать товар, введите его идентификатор (0 для возврата).");
                        string good_id = Console.ReadLine();
                        if (good_id != "0")
                        {
                            ChangeGood(good_id, goodsAdm);
                        }
                    }
                    else if (answer == "4")
                    {
                        Console.WriteLine(goodsAdm.GetOrdersForAdmin());
                        Console.WriteLine("Чтобы получить подробную информацию о заказе, введите его идентификатор (0 для возврата).");
                        string orderId = Console.ReadLine();
                        if (orderId != "0")
                        {
                            Console.WriteLine(goodsAdm.GetOrderByIdForAdmin(orderId));
                            Console.WriteLine("Чтобы изменить статус заказа, введите новый статус:\n" +
                                    "Processing, Delivering, Finished (0 до возврата)");
                            string newStatus = Console.ReadLine();
                            if (newStatus != "0")
                            {
                                Console.WriteLine(goodsAdm.ChangeOrderStatus(orderId, newStatus));
                            }
                        }
                    }
                }
            }
            // for user
            else 
            {
                Goods goods = (Goods)Activator.CreateInstance(typeof(Goods), new object[] { tcpUser.GetId()}, httpAttr);
                ILease leaseGoods = (ILease)goods.GetLifetimeService();
                MySponsor sponsorGoods = new MySponsor("goodshttp");
                
                leaseGoods.Register(sponsorGoods);
                while (true)
                {
                    Console.WriteLine("┌───────────────────────────────────────┐");
                    Console.WriteLine("│ 1 - Просмотреть все товары            │");
                    Console.WriteLine("│ 2 - Поиск товаров по категории        │");
                    Console.WriteLine("│ 3 - Просмотреть корзину               │");
                    Console.WriteLine("│ 4 - Просмотреть историю заказов       │");
                    Console.WriteLine("│ 5 - Просмотреть личную информацию     │");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("│ 0 - Выход                             │");
                    Console.ResetColor();
                    Console.WriteLine("└───────────────────────────────────────┘");
                    string answer = Console.ReadLine();
                    if (answer == "0")
                    {
                        return;
                    }
                    else if (answer == "1")
                    {
                        Console.WriteLine(goods.GetAllGoods());
                        Console.WriteLine("Чтобы добавить товар в корзину, введите его идентификатор (0 для возврата).");
                        string good_id = Console.ReadLine();
                        if (good_id != "0")
                        {
                            Console.WriteLine(goods.AddToCart(good_id));
                        }
                    }
                    else if (answer == "2")
                    {
                        Console.WriteLine("Категории:");
                        Console.WriteLine(goods.GetAllCategories());
                        Console.WriteLine("Введите название желаемой категории:");
                        string category = Console.ReadLine();
                        Console.WriteLine(goods.GetGoodsByCategory(category));
                        Console.WriteLine("Чтобы добавить товар в корзину, введите его идентификатор (0 для возврата).");
                        string good_id = Console.ReadLine();
                        if (good_id != "0")
                        {
                            Console.WriteLine(goods.AddToCart(good_id));
                        }
                    }
                    else if (answer == "3")
                    {
                        Console.WriteLine(goods.GetCart());
                        Console.WriteLine("Чтобы удалить товар из корзины, введите его идентификатор \n" +
                            "Чтобы сделать заказ, введите \"order\" (0 для возврата).");
                        string good_id = Console.ReadLine();
                        if (good_id == "order")
                        {
                            Console.WriteLine("Введите адрес заказа:");
                            string address = Console.ReadLine();
                            Console.WriteLine(goods.MakeOrder(address));
                        }
                        else if (good_id != "0")
                        {
                            Console.WriteLine(goods.RemoveFromCart(good_id));
                        }
                    }
                    else if (answer == "4")
                    {
                        Console.WriteLine(goods.GetOrders());
                        Console.WriteLine("Чтобы получить подробную информацию о заказе, введите его идентификатор (0 для возврата).");
                        string orderId = Console.ReadLine();
                        if (orderId != "0")
                        {
                            Console.WriteLine(goods.GetOrderById(orderId));
                        }
                    }
                    else if (answer == "5")
                    {
                        Console.WriteLine(tcpUser.ProfileInfo());
                        Console.WriteLine("┌───────────────────────────┐");
                        Console.WriteLine("│   РЕДАКТИРОВАНИЕ ПРОФИЛЯ  │");
                        Console.WriteLine("├───────────────────────────┤");
                        Console.WriteLine("│ 1 - Изменить имя          │");
                        Console.WriteLine("│ 2 - Изменить фамилию      │");
                        Console.WriteLine("│ 3 - Изменить отчество     │");
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine("│ 0 - Возврат               │");
                        Console.ResetColor();
                        Console.WriteLine("└───────────────────────────┘");
                        string editing = Console.ReadLine();
                        if (editing == "1")
                        {
                            Console.WriteLine("Введите новое имя:");
                            string firstName = Console.ReadLine();
                            Console.WriteLine(tcpUser.ChangeFirstName(firstName));
                        }
                        else if (editing == "2")
                        {
                            Console.WriteLine("Введите новую фамилию:");
                            string lastName = Console.ReadLine();
                            Console.WriteLine(tcpUser.ChangeLastName(lastName));
                        }
                        else if (editing == "3")
                        {
                            Console.WriteLine("Введите новое отчество:");
                            string middleName = Console.ReadLine();
                            Console.WriteLine(tcpUser.ChangeMiddleName(middleName));
                        }
                    }
                }
            }
        }
        public static void ChangeGood(string good_id, GoodsAdm obj)
        {
            Console.WriteLine("┌───────────────────────────┐");
            Console.WriteLine("│   РЕДАКТИРОВАНИЕ ТОВАРА   │");
            Console.WriteLine("├───────────────────────────┤");
            Console.WriteLine("│ 1 - Изменить название     │");
            Console.WriteLine("│ 2 - Изменить описание     │");
            Console.WriteLine("│ 3 - Изменить количество   │");
            Console.WriteLine("│ 4 - Изменить цену         │");
            Console.WriteLine("│ 5 - Изменить категорию    │");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("│ 0 - Возврат               │");
            Console.ResetColor();
            Console.WriteLine("└───────────────────────────┘");
            string change = Console.ReadLine();
            if (change != "0")
            {
                switch (change)
                {
                    case "1":
                        {
                            Console.WriteLine("Введите новое название:");
                            string newTitle = Console.ReadLine();
                            Console.WriteLine(obj.EditGoodTitle(good_id, newTitle));
                            break;
                        }
                    case "2":
                        {
                            Console.WriteLine("Введите новое описание:");
                            string newDescription = Console.ReadLine();
                            Console.WriteLine(obj.EditGoodDescription(good_id, newDescription));
                            break;
                        }
                    case "3":
                        {
                            Console.WriteLine("Введите новое количество:");
                            string newAmount = Console.ReadLine();
                            Console.WriteLine(obj.EditGoodAmount(good_id, newAmount));
                            break;
                        }
                    case "4":
                        {
                            Console.WriteLine("Введите новую цену:");
                            string newPrice = Console.ReadLine();
                            Console.WriteLine(obj.EditGoodPrice(good_id, newPrice));
                            break;
                        }
                    case "5":
                        {
                            Console.WriteLine("Введите новую категорию:");
                            string newCategory = Console.ReadLine();
                            Console.WriteLine(obj.EditGoodCategory(good_id, newCategory));
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Неверный выбор");
                            break;
                        }
                }
            }
        }
        static void ConfigureRemotingProgrammatically()
        {

            BinaryServerFormatterSinkProvider srvPrvdTCP = new BinaryServerFormatterSinkProvider();
            srvPrvdTCP.TypeFilterLevel = TypeFilterLevel.Full;
            BinaryClientFormatterSinkProvider clntPrvdTCP = new BinaryClientFormatterSinkProvider();
            Dictionary<string, string> proprtTCP = new Dictionary<string, string>();
            proprtTCP["port"] = "0";
            proprtTCP["secure"] = "true";
            proprtTCP["encryptionAlgorithm"] = "AES";
            proprtTCP["protectionLevel"] = "EncryptAndSign";
            TcpChannel channelTCP = new TcpChannel(proprtTCP, clntPrvdTCP, srvPrvdTCP);
            ChannelServices.RegisterChannel(channelTCP, false);


            BinaryServerFormatterSinkProvider srvPrvdHTTP = new BinaryServerFormatterSinkProvider();
            srvPrvdHTTP.TypeFilterLevel = TypeFilterLevel.Full;
            BinaryClientFormatterSinkProvider clntPrvdHTTP = new BinaryClientFormatterSinkProvider();
            Dictionary<string, string> proprtHTTP = new Dictionary<string, string>();
            proprtHTTP["port"] = "0";
            proprtHTTP["secure"] = "true";
            proprtHTTP["useSsl"] = "true";
            proprtHTTP["protectionLevel"] = "EncryptAndSign";
            HttpChannel channelHTTP = new HttpChannel(proprtHTTP, clntPrvdHTTP, srvPrvdHTTP);
            ChannelServices.RegisterChannel(channelHTTP, false);

        }
        public static string ReadPasswordHidden()
        {
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Remove(password.Length - 1);
                }
            }
            while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return password;
        }
    }
    public class MySponsor : MarshalByRefObject, ISponsor
    {
        private DateTime lastRenewal;
        int count = 0;
        ServerConsole serverConsole;
        string name;

        public MySponsor(string name) // ПОМЕНЯТЬ IP
        {
            serverConsole = (ServerConsole)Activator.GetObject(typeof(ServerConsole),
                "tcp://172.20.10.13:8086/ServerConsoleURI");
            serverConsole.SendConsoleMessage($"Спонсор {name} был создан");
            lastRenewal = DateTime.Now;
            this.name = name;
        }

        public TimeSpan Renewal(ILease lease)
        {
            count++;
            serverConsole.SendConsoleMessage($"Спонсор {name}: Renewal было вызвано {count} раз");
            serverConsole.SendConsoleMessage($"Спонсор {name}: Времени с последнего вызова: " + (DateTime.Now - lastRenewal).ToString() + "\n");
            lastRenewal = DateTime.Now;
            return TimeSpan.FromMinutes(1);
        }
    }
}
