using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace RemoteObjects
{
    public class User : MarshalByRefObject
    {
        private int userId = 0;
        private bool isAdmin = false;
        public User()
        {
            Console.WriteLine("Создан удаленный объект User");
        }
        ~User()
        {
            Console.WriteLine("Уничтожен удаленный объект User");
        }
        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();
            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromMinutes(1);
                lease.SponsorshipTimeout = TimeSpan.FromMinutes(3);
                lease.RenewOnCallTime = TimeSpan.FromSeconds(40);
            }
            return lease;
        }
        public bool userIsAdmin()
        {
            return isAdmin;
        }
        public int GetId()
        {
            return userId;
        }
        public string GetName()
        {
            if (userId == 0)
            {
                return "Вход не выполнен";
            }
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";
            using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
            {
                try
                {
                    sqlConnection.Open();

                    string query = "SELECT * FROM dbo.userData WHERE id = @id";
                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("id", userId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return (string)reader["name"];
                            }
                            else
                            {
                                reader.Close();
                                return "Нет пользователя с этим идентификатором";
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    return $"Ошибка в базе данных: {ex.Message}";
                }
            }
        }
            public string Authenticate(string email, string password)
            {
                byte[] passw = sha256_hash(password);

                string pattern = @"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$";
                Match match = Regex.Match(email, pattern);
                if ((!match.Success || email.Length > 50) && email != "admin")
                {
                    return "Неверный email адрес";
                }
                string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    try
                    {
                        sqlConnection.Open();

                        string query = "SELECT * FROM dbo.users WHERE email = @email";
                        using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                        {
                            cmd.Parameters.AddWithValue("email", email);

                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {

                                    byte[] storedPassword = (byte[])reader["password"];
                                    if (storedPassword.SequenceEqual(passw) == false)
                                    {
                                        return "Неверный пароль";
                                    }
                                    userId = (int)reader["id"];
                                    reader.Close();
                                    if (email == "admin")
                                    {
                                        isAdmin = true;
                                    }
                                    return "Вход успешный";
                                }
                                else
                                {
                                    reader.Close();
                                    return "Пользвоатель с данным email не найден";
                                }
                            }
                        }
                    }
                    catch (SqlException ex)
                    {
                        return $"Ошибка в базе данных: {ex.Message}";
                    }
                }
            }
        public string CreateUser(string email, string password, string password2, string name)
        {
            if (password.Length == 0)
            {
                return "Пароль не должен быть пустым";
            }
            if (password != password2)
            {
                return "Пароли не совпадают";
            }

            if (name.Length == 0)
            {
                return "Введите имя";
            }

            string pattern = @"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$";
            Match match = Regex.Match(email, pattern);
            if ((!match.Success || email.Length > 50))
            {
                return "Неверный email адрес";
            }

            byte[] passw = sha256_hash(password);

            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
            {
                try
                {
                    sqlConnection.Open();

                    string query = "SELECT * FROM dbo.users WHERE email = @email";
                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("email", email);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return "Пользователь с таким email уже существует";
                            }
                            else
                            {
                                reader.Close();

                                string query2 = "INSERT INTO users (email, password) VALUES (@email, @passw);" +
                                                "SELECT CAST(SCOPE_IDENTITY() AS INT);";
                                using (SqlCommand cmd2 = new SqlCommand(query2, sqlConnection))
                                {
                                    cmd2.Parameters.AddWithValue("@email", email);
                                    cmd2.Parameters.AddWithValue("@passw", passw);

                                    try
                                    {
                                        object result = cmd2.ExecuteScalar();

                                        if (result == null || result == DBNull.Value)
                                        {
                                            return "Не удалось создать пользователя";
                                        }

                                        userId = Convert.ToInt32(result);

                                        string query3 = "INSERT INTO userData (id, name) VALUES (@id, @name);";
                                        using (SqlCommand cmd3 = new SqlCommand(query3, sqlConnection))
                                        {
                                            cmd3.Parameters.AddWithValue("@id", userId);
                                            cmd3.Parameters.AddWithValue("@name", name);

                                            int rowsAffected = cmd3.ExecuteNonQuery();

                                            if (rowsAffected > 0)
                                            {
                                                return "Пользователь создан";
                                            }
                                            else
                                            {
                                                return "Пользователь создан с ошибками";
                                            }
                                        }
                                    }
                                    catch (SqlException ex)
                                    {
                                        return $"Ошибка в базе данных: {ex.Message}";
                                    }
                                    catch (InvalidCastException ex)
                                    {
                                        return $"Ошибка при конвертации идентификатора: {ex.Message}";
                                    }
                                }
                            }
                        }
                    }
                }
                catch (SqlException ex)
                {
                    return $"Ошибка в базе данных: {ex.Message}";
                }
            }

        }
        private byte[] sha256_hash(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash = sha256.ComputeHash(bytes);
                return hash;
            }
        }
        public string ProfileInfo()
        {
            if (userId == 0)
            {
                return "Вход не выполнен";
            }

            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    string query = "SELECT name, [last name], [middle name] FROM userData WHERE id = @user_id";

                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("@user_id", userId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string name = reader["name"].ToString();
                                string lastName = reader["last name"].ToString();
                                string middleName = reader["middle name"].ToString();

                                // Красивое оформление профиля
                                int boxWidth = Math.Max(
                                    name.Length + lastName.Length + middleName.Length + 10,
                                    30); // Минимальная ширина

                                string horizontalLine = new string('═', boxWidth+2);

                                result.AppendLine($"╔{horizontalLine}╗");
                                result.AppendLine($"║{" ПРОФИЛЬ ".PadLeft((boxWidth + 12) / 2).PadRight(boxWidth + 2)}║");
                                result.AppendLine($"╠{horizontalLine}╣");

                                if (!string.IsNullOrEmpty(lastName))
                                {
                                    result.AppendLine($"║ {"Фамилия:".PadRight(15)} {lastName.PadRight(boxWidth - 16)} ║");
                                }
                                else
                                {
                                    result.AppendLine($"║ {"Фамилия:".PadRight(15)} {"-".PadRight(boxWidth - 16)} ║");
                                }

                                //result.AppendLine($"║ {"Last Name:".PadRight(15)} {lastName.PadRight(boxWidth - 17)} ║");
                                result.AppendLine($"║ {"Имя:".PadRight(15)} {name.PadRight(boxWidth - 16)} ║");

                                if (!string.IsNullOrEmpty(middleName))
                                {
                                    result.AppendLine($"║ {"Отчество:".PadRight(15)} {middleName.PadRight(boxWidth - 16)} ║");
                                }
                                else
                                {
                                    result.AppendLine($"║ {"Отчество:".PadRight(15)} {"-".PadRight(boxWidth - 16)} ║");
                                }

                                result.AppendLine($"╚{horizontalLine}╝");
                            }
                            else
                            {
                                return "╔══════════════════════╗\n" +
                                       "║  Профиль не найден   ║\n" +
                                       "╚══════════════════════╝";
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string ChangeFirstName(string firstName)
        {
            if (userId == 0)
            {
                return "Вход не выполнен";
            }

            // Валидация ввода
            if (string.IsNullOrWhiteSpace(firstName))
            {
                return "Ошибка: имя не может быть пустой строкой";
            }

            if (firstName.Length > 50)
            {
                return "Ошибка: имя слишком длинное";
            }

            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    string updateQuery = "UPDATE userData SET name = @firstName WHERE id = @userId";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("@firstName", firstName.Trim());
                        cmd.Parameters.AddWithValue("@userId", userId);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            return $"Имя успешно изменено на {firstName}";
                        }
                        else
                        {
                            return "Ошибка: профиль не найден";
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message} ";
            }
        }
        public string ChangeLastName(string lastName)
        {
            if (userId == 0)
            {
                return "Вход не выполнен";
            }

            if (lastName.Length > 50)
            {
                return "Ошибка: фамилия слишком длинная";
            }

            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    string updateQuery = "UPDATE userData SET [last name] = @lastName WHERE id = @userId";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("@lastName", lastName.Trim());
                        cmd.Parameters.AddWithValue("@userId", userId);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            return $"Фамилия успешно изменена на {lastName}";
                        }
                        else
                        {
                            return "Ошибка: пользователь не найден";
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message} ";
            }
        }
        public string ChangeMiddleName(string middleName)
        {
            if (userId == 0)
            {
                return "Вход не выполнен";
            }

            if (middleName.Length > 50)
            {
                return "Ошибка: отчество слишком длинное";
            }

            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    string updateQuery = "UPDATE userData SET [middle name] = @middleName WHERE id = @userId";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("@middleName", middleName.Trim());
                        cmd.Parameters.AddWithValue("@userId", userId);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            return $"Отчество успешно изменено на {middleName}";
                        }
                        else
                        {
                            return "Ошибка: пользователь не найден";
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message} ";
            }
        }
    }

    public class Goods : MarshalByRefObject
    {
        private int userId;
        public Goods(int userId)
        {
            this.userId = userId;
            Console.WriteLine("Создан удаленный объект goods");
        }
        ~Goods()
        {
            Console.WriteLine("Уничтожен удаленный объект goods");
        }
        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();
            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromMinutes(1);
                lease.SponsorshipTimeout = TimeSpan.FromMinutes(3);
                lease.RenewOnCallTime = TimeSpan.FromSeconds(40);
            }
            return lease;
        }
        public string GetAllGoods()
        {
            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    // Запрос с JOIN для получения названия категории
                    string query = @"
                SELECT 
                    g.id,
                    g.title,
                    g.description,
                    g.amount,
                    g.price,
                    c.title AS category_name
                FROM dbo.goods g
                LEFT JOIN dbo.categories c ON g.category_id = c.id
                ORDER BY g.title";

                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        // Определяем ширину колонок (добавили колонку для ID)
                        int idWidth = 5;
                        int titleWidth = 30;
                        int descWidth = 30;
                        int categoryWidth = 15;
                        int amountWidth = 8;
                        int priceWidth = 10;

                        // Шапка таблицы (добавили колонку ID)
                        result.AppendLine("╔═══════╦════════════════════════════════╦════════════════════════════════╦═════════════════╦══════════╦════════════╗");
                        result.AppendLine("║ ID    ║ Название                       ║ Описание                       ║ Категория       ║ Кол-во   ║ Цена       ║");
                        result.AppendLine("╠═══════╬════════════════════════════════╬════════════════════════════════╬═════════════════╬══════════╬════════════╣");

                        // Данные товаров
                        while (reader.Read())
                        {
                            string id = reader["id"].ToString();
                            string title = reader["title"].ToString();
                            string description = reader["description"].ToString();
                            string category = reader.IsDBNull(reader.GetOrdinal("category_name")) ?
                                "N/A" : reader["category_name"].ToString();
                            string amount = reader["amount"].ToString();
                            string price = reader["price"].ToString();

                            result.AppendLine($"║ {id.PadLeft(idWidth)} ║ " +
                                            $"{Truncate(title, titleWidth).PadRight(titleWidth)} ║ " +
                                            $"{Truncate(description, descWidth).PadRight(descWidth)} ║ " +
                                            $"{Truncate(category, categoryWidth).PadRight(categoryWidth)} ║ " +
                                            $"{amount.PadLeft(amountWidth)} ║ " +
                                            $"{price.PadLeft(priceWidth)} ║");
                        }

                        // Подвал таблицы
                        result.AppendLine("╚═══════╩════════════════════════════════╩════════════════════════════════╩═════════════════╩══════════╩════════════╝");
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        private string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return new string(' ', maxLength);

            return value.Length > maxLength ?
                value.Substring(0, maxLength - 3) + "..." :
                value.PadRight(maxLength);
        }
        public string GetAllCategories()
        {
            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    string query = "SELECT title FROM dbo.categories ORDER BY title";
                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        // Определяем ширину таблицы
                        int tableWidth = 40;
                        string line = new string('═', tableWidth - 2);

                        // Шапка таблицы
                        result.AppendLine($"╔{line}╗");
                        result.AppendLine($"║{" КАТЕГОРИИ ".PadLeft((tableWidth + 14) / 2).PadRight(tableWidth - 2)}║");
                        result.AppendLine($"╠{line}╣");

                        if (!reader.HasRows)
                        {
                            result.AppendLine($"║{"Категории не найдены".PadLeft((tableWidth + 18) / 2).PadRight(tableWidth - 2)}║");
                        }
                        else
                        {
                            while (reader.Read())
                            {
                                string title = reader["title"].ToString();
                                result.AppendLine($"║ {Truncate(title, tableWidth - 4).PadRight(tableWidth - 4)} ║");
                            }
                        }

                        // Подвал таблицы
                        result.AppendLine($"╚{line}╝");
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string GetGoodsByCategory(string category)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";
            StringBuilder result = new StringBuilder();

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    // 1. Получаем ID категории
                    string query = "SELECT id FROM dbo.categories WHERE title = @title";
                    object categoryIdObj;

                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("@title", category);
                        categoryIdObj = cmd.ExecuteScalar();
                    }

                    if (categoryIdObj == null || categoryIdObj == DBNull.Value)
                    {
                        return "╔══════════════════════════════════╗\n" +
                               "║      Категория не найдена        ║\n" +
                               "╚══════════════════════════════════╝";
                    }

                    int category_id = (int)categoryIdObj;

                    // 2. Получаем товары этой категории
                    string query2 = @"
                SELECT 
                    g.id,
                    g.title,
                    g.description,
                    g.amount,
                    g.price,
                    c.title AS category_name
                FROM dbo.goods g
                LEFT JOIN dbo.categories c ON g.category_id = c.id
                WHERE g.category_id = @category_id
                ORDER BY g.title";

                    using (SqlCommand cmd2 = new SqlCommand(query2, sqlConnection))
                    {
                        cmd2.Parameters.AddWithValue("@category_id", category_id);

                        using (SqlDataReader reader = cmd2.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                return $"╔═════════════════════════════════════════════╗\n" +
                                       $"║ Нет товаров в категории{Truncate(category, 15)}' ║\n" +
                                       $"╚═════════════════════════════════════════════╝";
                            }

                            // Определяем ширину колонок
                            int idWidth = 5;
                            int titleWidth = 30;
                            int descWidth = 30;
                            int categoryWidth = 15;
                            int amountWidth = 8;
                            int priceWidth = 10;

                            // Шапка таблицы с ID
                            result.AppendLine("╔═══════╦════════════════════════════════╦════════════════════════════════╦═════════════════╦══════════╦════════════╗");
                            result.AppendLine("║ ID    ║ Название                       ║ Описание                       ║ Категория       ║ Кол-во   ║ Цена       ║");
                            result.AppendLine("╠═══════╬════════════════════════════════╬════════════════════════════════╬═════════════════╬══════════╬════════════╣");

                            while (reader.Read())
                            {
                                string id = reader["id"].ToString();
                                string title = reader["title"].ToString();
                                string description = reader["description"].ToString();
                                string categoryName = reader["category_name"].ToString();
                                string amount = reader["amount"].ToString();
                                string price = reader["price"].ToString();

                                result.AppendLine($"║ {id.PadLeft(idWidth)} ║ " +
                                                $"{Truncate(title, titleWidth).PadRight(titleWidth)} ║ " +
                                                $"{Truncate(description, descWidth).PadRight(descWidth)} ║ " +
                                                $"{Truncate(categoryName, categoryWidth).PadRight(categoryWidth)} ║ " +
                                                $"{amount.PadLeft(amountWidth)} ║ " +
                                                $"{price.PadLeft(priceWidth)} ║");
                            }

                            // Подвал таблицы
                            result.AppendLine("╚═══════╩════════════════════════════════╩════════════════════════════════╩═════════════════╩══════════╩════════════╝");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string GetCart()
        {
            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    string query = @"
                SELECT 
                    g.id,
                    g.title,
                    g.price,
                    c.amount,
                    (g.price * c.amount) AS total_price
                FROM dbo.cart c
                JOIN dbo.goods g ON c.good_id = g.id
                WHERE c.user_id = @user_id
                ORDER BY g.title";

                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("@user_id", userId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                return "╔══════════════════════════════════╗\n" +
                                       "║         Корзина пуста            ║\n" +
                                       "╚══════════════════════════════════╝";
                            }

                            // Определяем ширину колонок (добавили колонку для ID)
                            int idWidth = 5;
                            int titleWidth = 20;
                            int priceWidth = 10;
                            int quantityWidth = 8;
                            int totalWidth = 12;

                            // Шапка таблицы (добавили колонку ID)
                            result.AppendLine("╔═══════╦══════════════════════╦════════════╦══════════╦══════════════╗");
                            result.AppendLine("║ ID    ║ Название             ║ Цена       ║ Кол-во   ║ Всего        ║");
                            result.AppendLine("╠═══════╬══════════════════════╬════════════╬══════════╬══════════════╣");

                            decimal cartTotal = 0;

                            while (reader.Read())
                            {
                                string id = reader["id"].ToString();
                                string title = reader["title"].ToString();
                                decimal price = Convert.ToDecimal(reader["price"]);
                                int quantity = Convert.ToInt32(reader["amount"]);
                                decimal total = Convert.ToDecimal(reader["total_price"]);
                                cartTotal += total;

                                result.AppendLine($"║ {id.PadLeft(idWidth)} ║ " +
                                                $"{Truncate(title, titleWidth).PadRight(titleWidth)} ║ " +
                                                $"{price.ToString().PadLeft(priceWidth)} ║ " +
                                                $"{quantity.ToString().PadLeft(quantityWidth)} ║ " +
                                                $"{total.ToString().PadLeft(totalWidth)} ║");
                            }

                            // Подвал таблицы с итоговой суммой
                            result.AppendLine("╠═══════╩══════════════════════╩════════════╩══════════╩══════════════╣");
                            result.AppendLine($"║ {"Итого        :".PadRight(42)} {cartTotal.ToString().PadLeft(24)} ║");
                            result.AppendLine("╚═════════════════════════════════════════════════════════════════════╝");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string GetOrders()
        {
            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    // Получаем список заказов пользователя
                    string query = @"
                SELECT 
                    o.id,
                    o.date,
                    o.total,
                    o.status,
                    o.address,
                    COUNT(oi.order_id) AS items_count
                FROM orders o
                LEFT JOIN orderItems oi ON o.id = oi.order_id
                WHERE o.user_id = @user_id
                GROUP BY o.id, o.date, o.total, o.status, o.address
                ORDER BY o.date DESC";

                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("@user_id", userId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                return "╔══════════════════════════════════╗\n" +
                                       "║        Заказы не найдены         ║\n" +
                                       "╚══════════════════════════════════╝";
                            }

                            // Определяем ширину колонок
                            int idWidth = 8;
                            int dateWidth = 23;
                            int totalWidth = 12;
                            int statusWidth = 15;
                            int itemsWidth = 7;
                            int addressWidth = 30;

                            // Шапка таблицы
                            result.AppendLine("╔══════════╦═════════════════════════╦══════════════╦═════════════════╦═════════╦════════════════════════════════╗");
                            result.AppendLine("║ Order ID ║ Дата                    ║ Кол-во товар ║ Статус          ║ Товары  ║ Адрес доставки                 ║");
                            result.AppendLine("╠══════════╬═════════════════════════╬══════════════╬═════════════════╬═════════╬════════════════════════════════╣");

                            while (reader.Read())
                            {
                                int orderId = reader.GetInt32(0);
                                DateTime orderDate = reader.GetDateTime(1);
                                decimal total = reader.GetInt32(2);
                                string status = reader.GetString(3);
                                string address = reader.GetString(4);
                                int itemsCount = reader.GetInt32(5);

                                result.AppendLine($"║ {orderId.ToString().PadLeft(idWidth)} ║ " +
                                                $"{orderDate.ToString("yyyy-MM-dd HH:mm").PadRight(dateWidth)} ║ " +
                                                $"{total.ToString().PadLeft(totalWidth)} ║ " +
                                                $"{status.PadRight(statusWidth)} ║ " +
                                                $"{itemsCount.ToString().PadLeft(itemsWidth)} ║ " +
                                                $"{Truncate(address, addressWidth).PadRight(addressWidth)} ║");
                            }

                            result.AppendLine("╚══════════╩═════════════════════════╩══════════════╩═════════════════╩═════════╩════════════════════════════════╝");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string GetOrderById(string orderId)
        {
            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    // 1. Получаем основную информацию о заказе
                    string orderQuery = @"
                SELECT 
                    o.user_id,
                    o.date,
                    o.total,
                    o.status,
                    o.address,
                    u.email
                FROM orders o
                JOIN users u ON o.user_id = u.id
                WHERE o.id = @order_id";

                    DateTime orderDate = DateTime.MinValue;
                    decimal orderTotal = 0;
                    string orderStatus = "";
                    string orderAddress = "";
                    string userEmail = "";
                    int orderUserId = 0;

                    using (SqlCommand orderCmd = new SqlCommand(orderQuery, sqlConnection))
                    {
                        orderCmd.Parameters.AddWithValue("@order_id", orderId);

                        using (SqlDataReader orderReader = orderCmd.ExecuteReader())
                        {
                            if (!orderReader.HasRows)
                            {
                                return $"╔══════════════════════════════════╗\n" +
                                       $"║ Заказ {orderId} не найден        ║\n" +
                                       $"╚══════════════════════════════════╝";
                            }

                            orderReader.Read();
                            orderUserId = orderReader.GetInt32(0);
                            orderDate = orderReader.GetDateTime(1);
                            orderTotal = orderReader.GetInt32(2);
                            orderStatus = orderReader.GetString(3);
                            orderAddress = orderReader.GetString(4);
                            userEmail = orderReader.GetString(5);
                        }
                    }

                    // 2. Проверяем права доступа
                    if (orderUserId != userId)
                    {
                        return "╔══════════════════════════════════╗\n" +
                               "║ Доступ запрещен: вам можно только║\n" +
                               "║ просматривать свои заказы        ║\n" +
                               "╚══════════════════════════════════╝";
                    }

                    // 3. Получаем детали заказа
                    string detailsQuery = @"
                SELECT 
                    g.id,
                    g.title,
                    oi.amount,
                    g.price,
                    (oi.amount * g.price) AS item_total
                FROM orderItems oi
                JOIN goods g ON oi.good_id = g.id
                WHERE oi.order_id = @order_id
                ORDER BY g.title";

                    using (SqlCommand detailsCmd = new SqlCommand(detailsQuery, sqlConnection))
                    {
                        detailsCmd.Parameters.AddWithValue("@order_id", orderId);

                        // Формируем заголовок с полной информацией о заказе
                        result.AppendLine("╔════════════════════════════════════════════════════════════╗");
                        result.AppendLine($"║ ЗАКАЗ #{orderId.ToString().PadRight(51)} ║");
                        result.AppendLine($"║ Дата: {orderDate.ToString("yyyy-MM-dd HH:mm").PadRight(52)} ║");
                        result.AppendLine($"║ Статус: {orderStatus.PadRight(50)} ║");
                        result.AppendLine($"║ Почта: {Truncate(userEmail, 45).PadRight(46)} ║");
                        result.AppendLine($"║ Адрес доставки: {Truncate(orderAddress, 40).PadRight(40)} ║");
                        result.AppendLine($"║ Общая сумма: {orderTotal.ToString().PadLeft(44)} ║");
                        result.AppendLine("╠════════════════════════════════════════════════════════════╣");

                        using (SqlDataReader detailsReader = detailsCmd.ExecuteReader())
                        {
                            if (detailsReader.HasRows)
                            {
                                result.AppendLine("║                     ДЕТАЛИ ЗАКАЗА                     ║");
                                result.AppendLine("╠══════╦══════════════════════╦══════════╦══════════════╗");
                                result.AppendLine("║ ID   ║ Товар                ║ Кол-во   ║ Сумма        ║");
                                result.AppendLine("╠══════╬══════════════════════╬══════════╬══════════════╣");

                                while (detailsReader.Read())
                                {
                                    string goodId = detailsReader["id"].ToString();
                                    string title = detailsReader["title"].ToString();
                                    int amount = Convert.ToInt32(detailsReader["amount"]);
                                    int total = Convert.ToInt32(detailsReader["item_total"]);

                                    result.AppendLine($"║ {goodId.PadLeft(4)} ║ " +
                                                    $"{Truncate(title, 20).PadRight(20)} ║ " +
                                                    $"{amount.ToString().PadLeft(8)} ║ " +
                                                    $"{total.ToString().PadLeft(12)} ║");
                                }

                                result.AppendLine("╠══════╩══════════════════════╩══════════╩══════════════╣");
                                result.AppendLine($"║ {"ИТОГО:".PadRight(30)} {orderTotal.ToString().PadLeft(22)} ║");
                                result.AppendLine("╚═══════════════════════════════════════════════════════╝");
                            }
                            else
                            {
                                result.AppendLine("║              Товары не найдены                   ║");
                                result.AppendLine("╚══════════════════════════════════════════════════╝");
                            }
                        }
                    }
                }
            }
            catch (FormatException)
            {
                return "Неверный формат идентификатора";
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string AddToCart(string good_id)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверяем валидность good_id
                if (!int.TryParse(good_id, out int goodId))
                {
                    return "Неверный формат идентификатора";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        // 1. Проверяем существование товара и его количество
                        string checkGoodQuery = "SELECT amount FROM goods WHERE id = @good_id";
                        int availableAmount = 0;

                        using (SqlCommand checkCmd = new SqlCommand(checkGoodQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@good_id", goodId);

                            using (SqlDataReader reader = checkCmd.ExecuteReader())
                            {
                                if (!reader.HasRows)
                                {
                                    transaction.Rollback();
                                    return "Товар не найден";
                                }

                                reader.Read();
                                availableAmount = reader.GetInt32(0);
                            }
                        }

                        if (availableAmount <= 0)
                        {
                            transaction.Rollback();
                            return "Товар закончился";
                        }

                        // 2. Проверяем, есть ли уже такой товар в корзине
                        string checkCartQuery = "SELECT amount FROM cart WHERE user_id = @user_id AND good_id = @good_id";
                        int currentQuantity = 0;

                        using (SqlCommand checkCartCmd = new SqlCommand(checkCartQuery, sqlConnection, transaction))
                        {
                            checkCartCmd.Parameters.AddWithValue("@user_id", userId);
                            checkCartCmd.Parameters.AddWithValue("@good_id", goodId);

                            object result = checkCartCmd.ExecuteScalar();
                            if (result != null)
                            {
                                currentQuantity = Convert.ToInt32(result);
                            }
                        }

                        // 3. Добавляем/обновляем товар в корзине
                        if (currentQuantity > 0)
                        {
                            // Увеличиваем количество, если товар уже в корзине
                            if (currentQuantity >= availableAmount)
                            {
                                transaction.Rollback();
                                return "Максимальное количество достигнуто";
                            }

                            string updateQuery = "UPDATE cart SET amount = amount + 1 WHERE user_id = @user_id AND good_id = @good_id";
                            using (SqlCommand updateCmd = new SqlCommand(updateQuery, sqlConnection, transaction))
                            {
                                updateCmd.Parameters.AddWithValue("@user_id", userId);
                                updateCmd.Parameters.AddWithValue("@good_id", goodId);
                                updateCmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // Добавляем новый товар в корзину
                            string insertQuery = "INSERT INTO cart (user_id, good_id, amount) VALUES (@user_id, @good_id, 1)";
                            using (SqlCommand insertCmd = new SqlCommand(insertQuery, sqlConnection, transaction))
                            {
                                insertCmd.Parameters.AddWithValue("@user_id", userId);
                                insertCmd.Parameters.AddWithValue("@good_id", goodId);
                                insertCmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        return "Товар успешно добавлен в корзину";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка добавления в корзину: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string RemoveFromCart(string good_id)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверяем валидность good_id
                if (!int.TryParse(good_id, out int goodId))
                {
                    return "Неверный формат идентификатора";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        // 1. Проверяем текущее количество товара в корзине
                        string checkQuery = @"
                    SELECT amount 
                    FROM cart 
                    WHERE user_id = @user_id AND good_id = @good_id";

                        int currentQuantity = 0;

                        using (SqlCommand checkCmd = new SqlCommand(checkQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@user_id", userId);
                            checkCmd.Parameters.AddWithValue("@good_id", goodId);

                            object result = checkCmd.ExecuteScalar();
                            if (result == null)
                            {
                                transaction.Rollback();
                                return "Товара не найден в корзине";
                            }
                            currentQuantity = Convert.ToInt32(result);
                        }

                        // 2. Если количество = 1 - удаляем товар полностью
                        if (currentQuantity == 1)
                        {
                            string deleteQuery = @"
                        DELETE FROM cart 
                        WHERE user_id = @user_id AND good_id = @good_id";

                            using (SqlCommand deleteCmd = new SqlCommand(deleteQuery, sqlConnection, transaction))
                            {
                                deleteCmd.Parameters.AddWithValue("@user_id", userId);
                                deleteCmd.Parameters.AddWithValue("@good_id", goodId);
                                deleteCmd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // 3. Если количество > 1 - уменьшаем на 1
                            string updateQuery = @"
                        UPDATE cart 
                        SET amount = amount - 1 
                        WHERE user_id = @user_id AND good_id = @good_id";

                            using (SqlCommand updateCmd = new SqlCommand(updateQuery, sqlConnection, transaction))
                            {
                                updateCmd.Parameters.AddWithValue("@user_id", userId);
                                updateCmd.Parameters.AddWithValue("@good_id", goodId);
                                updateCmd.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                        return "Товар успешно удален из корзины";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка удаления из корзины: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string MakeOrder(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return "Ошибка: адрес не может быть пустой строкой";
            }
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";
            StringBuilder result = new StringBuilder();

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        // 1. Получаем содержимое корзины и считаем общую сумму
                        decimal totalAmount = 0;
                        var cartItems = new List<CartItem>();

                        string getCartQuery = @"
                    SELECT g.id, g.price, c.amount, g.amount as stock_amount 
                    FROM cart c 
                    JOIN goods g ON c.good_id = g.id 
                    WHERE c.user_id = @user_id";

                        using (var cmd = new SqlCommand(getCartQuery, sqlConnection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@user_id", userId);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var item = new CartItem
                                    {
                                        GoodId = reader.GetInt32(0),
                                        Price = reader.GetInt32(1),
                                        Quantity = reader.GetInt32(2),
                                        StockAmount = reader.GetInt32(3)
                                    };

                                    // Проверка наличия товара на складе
                                    if (item.Quantity > item.StockAmount)
                                    {
                                        transaction.Rollback();
                                        return $"Недостаточно товара на складе с идентификатором {item.GoodId}";
                                    }

                                    cartItems.Add(item);
                                    totalAmount += item.Price * item.Quantity;
                                }
                            }
                        }

                        if (cartItems.Count == 0)
                        {
                            transaction.Rollback();
                            return "Невозможно создать заказ: корзина пуста";
                        }

                        // 2. Создаем запись заказа
                        string createOrderQuery = @"
                    INSERT INTO orders (user_id, date, total, status, address) 
                    VALUES (@user_id, @date, @total, 'Processing', @address);
                    SELECT SCOPE_IDENTITY();";

                        int orderId;
                        using (var cmd = new SqlCommand(createOrderQuery, sqlConnection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@user_id", userId);
                            cmd.Parameters.AddWithValue("@date", DateTime.Now);
                            cmd.Parameters.AddWithValue("@total", totalAmount);
                            cmd.Parameters.AddWithValue("@address", address);
                            orderId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // 3. Добавляем товары в заказ и уменьшаем количество на складе
                        foreach (var item in cartItems)
                        {
                            // Добавляем в orderItems
                            string addItemQuery = @"
                        INSERT INTO orderItems (order_id, good_id, amount)
                        VALUES (@order_id, @good_id, @amount)";

                            using (var cmd = new SqlCommand(addItemQuery, sqlConnection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@order_id", orderId);
                                cmd.Parameters.AddWithValue("@good_id", item.GoodId);
                                cmd.Parameters.AddWithValue("@amount", item.Quantity);
                                cmd.ExecuteNonQuery();
                            }

                            // Уменьшаем количество на складе
                            string updateStockQuery = @"
                        UPDATE goods 
                        SET amount = amount - @amount 
                        WHERE id = @good_id";

                            using (var cmd = new SqlCommand(updateStockQuery, sqlConnection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@good_id", item.GoodId);
                                cmd.Parameters.AddWithValue("@amount", item.Quantity);
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 4. Очищаем корзину
                        string clearCartQuery = "DELETE FROM cart WHERE user_id = @user_id";
                        using (var cmd = new SqlCommand(clearCartQuery, sqlConnection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@user_id", userId);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        return $"Заказ #{orderId} создан успешно! Итого: {totalAmount}";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка в создании заказа: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
    }

    public class GoodsAdm: MarshalByRefObject
    {
        public GoodsAdm()
        {
            Console.WriteLine("Создан удаленный объект goodsAdm");
        }
        ~GoodsAdm()
        {
            Console.WriteLine("Уничтожен удаленный объект goodsAdm");
        }
        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();
            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromMinutes(1);
                lease.SponsorshipTimeout = TimeSpan.FromMinutes(3);
                lease.RenewOnCallTime = TimeSpan.FromSeconds(40);
            }
            return lease;
        }
        private string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return new string(' ', maxLength);

            return value.Length > maxLength ?
                value.Substring(0, maxLength - 3) + "..." :
                value.PadRight(maxLength);
        }
        public string GetGoodsByCategory(string category)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";
            StringBuilder result = new StringBuilder();

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    // 1. Получаем ID категории
                    string query = "SELECT id FROM dbo.categories WHERE title = @title";
                    object categoryIdObj;

                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    {
                        cmd.Parameters.AddWithValue("@title", category);
                        categoryIdObj = cmd.ExecuteScalar();
                    }

                    if (categoryIdObj == null || categoryIdObj == DBNull.Value)
                    {
                        return "╔══════════════════════════════════╗\n" +
                               "║      Категория не найдена        ║\n" +
                               "╚══════════════════════════════════╝";
                    }

                    int category_id = (int)categoryIdObj;

                    // 2. Получаем товары этой категории
                    string query2 = @"
                SELECT 
                    g.id,
                    g.title,
                    g.description,
                    g.amount,
                    g.price,
                    c.title AS category_name
                FROM dbo.goods g
                LEFT JOIN dbo.categories c ON g.category_id = c.id
                WHERE g.category_id = @category_id
                ORDER BY g.title";

                    using (SqlCommand cmd2 = new SqlCommand(query2, sqlConnection))
                    {
                        cmd2.Parameters.AddWithValue("@category_id", category_id);

                        using (SqlDataReader reader = cmd2.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                return $"╔══════════════════════════════════╗\n" +
                                       $"║ Нет товаров в категории'{Truncate(category, 15)}' ║\n" +
                                       $"╚══════════════════════════════════╝";
                            }

                            // Определяем ширину колонок
                            int idWidth = 5;
                            int titleWidth = 30;
                            int descWidth = 30;
                            int categoryWidth = 15;
                            int amountWidth = 8;
                            int priceWidth = 10;

                            // Шапка таблицы с ID
                            result.AppendLine("╔═══════╦════════════════════════════════╦════════════════════════════════╦═════════════════╦══════════╦════════════╗");
                            result.AppendLine("║ ID    ║ Название                       ║ Описание                       ║ Категория       ║ Кол-во   ║ Цена       ║");
                            result.AppendLine("╠═══════╬════════════════════════════════╬════════════════════════════════╬═════════════════╬══════════╬════════════╣");

                            while (reader.Read())
                            {
                                string id = reader["id"].ToString();
                                string title = reader["title"].ToString();
                                string description = reader["description"].ToString();
                                string categoryName = reader["category_name"].ToString();
                                string amount = reader["amount"].ToString();
                                string price = reader["price"].ToString();

                                result.AppendLine($"║ {id.PadLeft(idWidth)} ║ " +
                                                $"{Truncate(title, titleWidth).PadRight(titleWidth)} ║ " +
                                                $"{Truncate(description, descWidth).PadRight(descWidth)} ║ " +
                                                $"{Truncate(categoryName, categoryWidth).PadRight(categoryWidth)} ║ " +
                                                $"{amount.PadLeft(amountWidth)} ║ " +
                                                $"{price.PadLeft(priceWidth)} ║");
                            }

                            // Подвал таблицы
                            result.AppendLine("╚═══════╩════════════════════════════════╩════════════════════════════════╩═════════════════╩══════════╩════════════╝");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string GetAllGoods()
        {
            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    // Запрос с JOIN для получения названия категории
                    string query = @"
                SELECT 
                    g.id,
                    g.title,
                    g.description,
                    g.amount,
                    g.price,
                    c.title AS category_name
                FROM dbo.goods g
                LEFT JOIN dbo.categories c ON g.category_id = c.id
                ORDER BY g.title";

                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        // Определяем ширину колонок (добавили колонку для ID)
                        int idWidth = 5;
                        int titleWidth = 30;
                        int descWidth = 30;
                        int categoryWidth = 15;
                        int amountWidth = 8;
                        int priceWidth = 10;

                        // Шапка таблицы (добавили колонку ID)
                        result.AppendLine("╔═══════╦════════════════════════════════╦════════════════════════════════╦═════════════════╦══════════╦════════════╗");
                        result.AppendLine("║ ID    ║ Название                       ║ Описание                       ║ Категория       ║ Кол-во   ║ Цена       ║");
                        result.AppendLine("╠═══════╬════════════════════════════════╬════════════════════════════════╬═════════════════╬══════════╬════════════╣");

                        // Данные товаров
                        while (reader.Read())
                        {
                            string id = reader["id"].ToString();
                            string title = reader["title"].ToString();
                            string description = reader["description"].ToString();
                            string category = reader.IsDBNull(reader.GetOrdinal("category_name")) ?
                                "N/A" : reader["category_name"].ToString();
                            string amount = reader["amount"].ToString();
                            string price = reader["price"].ToString();

                            result.AppendLine($"║ {id.PadLeft(idWidth)} ║ " +
                                            $"{Truncate(title, titleWidth).PadRight(titleWidth)} ║ " +
                                            $"{Truncate(description, descWidth).PadRight(descWidth)} ║ " +
                                            $"{Truncate(category, categoryWidth).PadRight(categoryWidth)} ║ " +
                                            $"{amount.PadLeft(amountWidth)} ║ " +
                                            $"{price.PadLeft(priceWidth)} ║");
                        }

                        // Подвал таблицы
                        result.AppendLine("╚═══════╩════════════════════════════════╩════════════════════════════════╩═════════════════╩══════════╩════════════╝");
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string EditGoodTitle(string good_id, string newTitle)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверяем валидность good_id
                if (!int.TryParse(good_id, out int goodId))
                {
                    return "неверный формат идентификатора";
                }
                if (newTitle == "")
                {
                    return "Название не может быть пустым";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        string checkGoodQuery = "SELECT * FROM goods WHERE id = @id";

                        using (SqlCommand checkCmd = new SqlCommand(checkGoodQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@id", goodId);

                            using (SqlDataReader reader = checkCmd.ExecuteReader())
                            {
                                if (!reader.HasRows)
                                {
                                    reader.Close();
                                    transaction.Rollback();
                                    return "Товар не найден";
                                }
                                reader.Close();
                                string updateQuery = "UPDATE goods SET title = @newTitle WHERE id = @id";
                                using (SqlCommand updateCmd = new SqlCommand(updateQuery, sqlConnection, transaction))
                                {
                                    updateCmd.Parameters.AddWithValue("@id", goodId);
                                    updateCmd.Parameters.AddWithValue("@newTitle", newTitle);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return "Название товара успешно изменено";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка при смене названия: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string EditGoodDescription(string good_id, string newDescription)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверяем валидность good_id
                if (!int.TryParse(good_id, out int goodId))
                {
                    return "Неверный формат идентификатора";
                }
                if (newDescription == "")
                {
                    return "Описание не может быть пустым";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        string checkGoodQuery = "SELECT * FROM goods WHERE id = @id";

                        using (SqlCommand checkCmd = new SqlCommand(checkGoodQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@id", goodId);

                            using (SqlDataReader reader = checkCmd.ExecuteReader())
                            {
                                if (!reader.HasRows)
                                {
                                    reader.Close();
                                    transaction.Rollback();
                                    return "Товар не найден";
                                }
                                reader.Close();
                                string updateQuery = "UPDATE goods SET description = @newDescription WHERE id = @id";
                                using (SqlCommand updateCmd = new SqlCommand(updateQuery, sqlConnection, transaction))
                                {
                                    updateCmd.Parameters.AddWithValue("@id", goodId);
                                    updateCmd.Parameters.AddWithValue("@newDescription", newDescription);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return "Описание успешно изменено";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка при смене описания: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string EditGoodAmount(string good_id, string newAmount)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверяем валидность good_id
                if (!int.TryParse(good_id, out int goodId))
                {
                    return "Неверный формат идентификатора";
                }
                if (!int.TryParse(newAmount, out int newAmount1))
                {
                    return "Неверный формат количества";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        string checkGoodQuery = "SELECT * FROM goods WHERE id = @id";

                        using (SqlCommand checkCmd = new SqlCommand(checkGoodQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@id", goodId);

                            using (SqlDataReader reader = checkCmd.ExecuteReader())
                            {
                                if (!reader.HasRows)
                                {
                                    reader.Close();
                                    transaction.Rollback();
                                    return "Товар не найден";
                                }
                                reader.Close();
                                string updateQuery = "UPDATE goods SET amount = @newAmount WHERE id = @id";
                                using (SqlCommand updateCmd = new SqlCommand(updateQuery, sqlConnection, transaction))
                                {
                                    updateCmd.Parameters.AddWithValue("@id", goodId);
                                    updateCmd.Parameters.AddWithValue("@newAmount", newAmount1);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return "Количество успешно изменено";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка при смене количества: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string EditGoodPrice(string good_id, string newPrice)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверяем валидность good_id
                if (!int.TryParse(good_id, out int goodId))
                {
                    return "Неверный формат идентификатора";
                }
                if (!int.TryParse(newPrice, out int newPrice1))
                {
                    return "Неверный формат цены";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        string checkGoodQuery = "SELECT * FROM goods WHERE id = @id";

                        using (SqlCommand checkCmd = new SqlCommand(checkGoodQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@id", goodId);

                            using (SqlDataReader reader = checkCmd.ExecuteReader())
                            {
                                if (!reader.HasRows)
                                {
                                    reader.Close();
                                    transaction.Rollback();
                                    return "Товар не найден";
                                }
                                reader.Close();
                                string updateQuery = "UPDATE goods SET price = @newPrice WHERE id = @id";
                                using (SqlCommand updateCmd = new SqlCommand(updateQuery, sqlConnection, transaction))
                                {
                                    updateCmd.Parameters.AddWithValue("@id", goodId);
                                    updateCmd.Parameters.AddWithValue("@newPrice", newPrice1);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return "Цена успешно изменена";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка при изменении цены: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string EditGoodCategory(string good_id, string newCategory)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверяем валидность good_id
                if (!int.TryParse(good_id, out int goodId))
                {
                    return "Неверный формат идентификатора";
                }

                if (newCategory == "")
                {
                    return "Название категории не может быть пустым";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        int categoryId;

                        // 1. Проверяем существование категории
                        string checkCategoryQuery = "SELECT id FROM categories WHERE title = @title";
                        using (SqlCommand cmd = new SqlCommand(checkCategoryQuery, sqlConnection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@title", newCategory);
                            object result = cmd.ExecuteScalar();

                            if (result == null)
                            {
                                // 2. Категории нет - создаем новую
                                string createCategoryQuery = "INSERT INTO categories (title) VALUES (@title); SELECT SCOPE_IDENTITY();";
                                using (SqlCommand createCmd = new SqlCommand(createCategoryQuery, sqlConnection, transaction))
                                {
                                    createCmd.Parameters.AddWithValue("@title", newCategory);
                                    categoryId = Convert.ToInt32(createCmd.ExecuteScalar());
                                }
                            }
                            else
                            {
                                categoryId = Convert.ToInt32(result);
                            }
                        }

                        // 3. Обновляем категорию товара
                        string updateGoodQuery = "UPDATE goods SET category_id = @category_id WHERE id = @good_id";
                        using (SqlCommand updateCmd = new SqlCommand(updateGoodQuery, sqlConnection, transaction))
                        {
                            updateCmd.Parameters.AddWithValue("@good_id", goodId);
                            updateCmd.Parameters.AddWithValue("@category_id", categoryId);
                            int rowsAffected = updateCmd.ExecuteNonQuery();

                            if (rowsAffected == 0)
                            {
                                transaction.Rollback();
                                return "Товар не найден";
                            }
                        }

                        transaction.Commit();
                        return $"Категория успешно изменена на '{newCategory}'";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка при изменении категории: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string GetAllCategoriesForAdmin()
        {
            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    string query = "SELECT id, title FROM dbo.categories ORDER BY id";
                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        // Определяем ширину колонок
                        int idWidth = 5;
                        int titleWidth = 30;
                        int tableWidth = idWidth + titleWidth + 7; // 7 - дополнительные символы для оформления

                        // Шапка таблицы
                        string line = new string('═', tableWidth - 2);
                        result.AppendLine($"╔{line}╗");
                        result.AppendLine($"║{"КАТЕГОРИИ ".PadLeft((tableWidth + 20) / 2).PadRight(tableWidth - 2)}║");
                        result.AppendLine($"╠═══════╦{new string('═', titleWidth + 2)}╣");
                        result.AppendLine($"║ ID    ║ {"Category Name".PadRight(titleWidth)} ║");
                        result.AppendLine($"╠═══════╬{new string('═', titleWidth + 2)}╣");

                        if (!reader.HasRows)
                        {
                            result.AppendLine($"║ {"Категории не найдены".PadLeft((tableWidth + 18) / 2).PadRight(tableWidth - 4)} ║");
                        }
                        else
                        {
                            while (reader.Read())
                            {
                                string id = reader["id"].ToString();
                                string title = reader["title"].ToString();
                                result.AppendLine($"║ {id.PadLeft(idWidth)} ║ {Truncate(title, titleWidth).PadRight(titleWidth)} ║");
                            }
                        }

                        // Подвал таблицы
                        result.AppendLine($"╚═══════╩{new string('═', titleWidth + 2)}╝");
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string EditCategory(string cat_id, string newTitle)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверяем валидность good_id
                if (!int.TryParse(cat_id, out int goodId))
                {
                    return "неверный формат идентификатора";
                }
                if (newTitle == "")
                {
                    return "Название не может быть пустым";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        string checkGoodQuery = "SELECT * FROM categories WHERE id = @id";

                        using (SqlCommand checkCmd = new SqlCommand(checkGoodQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@id", goodId);

                            using (SqlDataReader reader = checkCmd.ExecuteReader())
                            {
                                if (!reader.HasRows)
                                {
                                    reader.Close();
                                    transaction.Rollback();
                                    return "Категория не найдена";
                                }
                                reader.Close();
                                string updateQuery = "UPDATE categories SET title = @newTitle WHERE id = @id";
                                using (SqlCommand updateCmd = new SqlCommand(updateQuery, sqlConnection, transaction))
                                {
                                    updateCmd.Parameters.AddWithValue("@id", goodId);
                                    updateCmd.Parameters.AddWithValue("@newTitle", newTitle);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return "Название категории успешно изменено";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка при изменении названия: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string GetOrdersForAdmin()
        {
            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    // Получаем список всех заказов с адресами
                    string query = @"
                SELECT 
                    o.id,
                    o.date,
                    o.total,
                    o.status,
                    o.address,
                    u.email,
                    COUNT(oi.order_id) AS items_count
                FROM orders o
                LEFT JOIN orderItems oi ON o.id = oi.order_id
                JOIN users u ON o.user_id = u.id
                GROUP BY o.id, o.date, o.total, o.status, o.address, u.email
                ORDER BY o.date DESC";

                    using (SqlCommand cmd = new SqlCommand(query, sqlConnection))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                return "╔══════════════════════════════════╗\n" +
                                       "║        Заказы не найдены         ║\n" +
                                       "╚══════════════════════════════════╝";
                            }

                            // Определяем ширину колонок
                            int idWidth = 8;
                            int dateWidth = 19;
                            int totalWidth = 12;
                            int statusWidth = 15;
                            int itemsWidth = 7;
                            int addressWidth = 25;
                            int emailWidth = 25;

                            // Шапка таблицы
                            result.AppendLine("╔══════════╦═════════════════════╦══════════════╦═════════════════╦═════════╦═══════════════════════════╦═══════════════════════════╗");
                            result.AppendLine("║ ID заказа║ Дата                ║ Общее кол-во ║ Статус          ║ Товары  ║ Адрес доставки            ║ Почта пользователя        ║");
                            result.AppendLine("╠══════════╬═════════════════════╬══════════════╬═════════════════╬═════════╬═══════════════════════════╬═══════════════════════════╣");

                            while (reader.Read())
                            {
                                int orderId = reader.GetInt32(0);
                                DateTime orderDate = reader.GetDateTime(1);
                                decimal total = reader.GetInt32(2);
                                string status = reader.GetString(3);
                                string address = reader.GetString(4);
                                string email = reader.GetString(5);
                                int itemsCount = reader.GetInt32(6);

                                result.AppendLine($"║ {orderId.ToString().PadLeft(idWidth)} ║ " +
                                                $"{orderDate.ToString("yyyy-MM-dd HH:mm").PadRight(dateWidth)} ║ " +
                                                $"{total.ToString().PadLeft(totalWidth)} ║ " +
                                                $"{status.PadRight(statusWidth)} ║ " +
                                                $"{itemsCount.ToString().PadLeft(itemsWidth)} ║ " +
                                                $"{Truncate(address, addressWidth).PadRight(addressWidth)} ║ " +
                                                $"{Truncate(email, emailWidth).PadRight(emailWidth)} ║");
                            }

                            result.AppendLine("╚══════════╩═════════════════════╩══════════════╩═════════════════╩═════════╩═══════════════════════════╩═══════════════════════════╝");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string GetOrderByIdForAdmin(string orderId)
        {
            StringBuilder result = new StringBuilder();
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();

                    // 1. Получаем основную информацию о заказе и пользователе
                    string orderQuery = @"
        SELECT 
            o.id,
            o.date,
            o.total,
            o.status,
            u.email,
            o.address
        FROM orders o
        JOIN users u ON o.user_id = u.id
        WHERE o.id = @order_id";

                    DateTime orderDate = DateTime.MinValue;
                    decimal orderTotal = 0;
                    string orderStatus = "";
                    string userEmail = "";
                    string orderAddress = "";

                    using (SqlCommand orderCmd = new SqlCommand(orderQuery, sqlConnection))
                    {
                        orderCmd.Parameters.AddWithValue("@order_id", orderId);

                        using (SqlDataReader orderReader = orderCmd.ExecuteReader())
                        {
                            if (!orderReader.HasRows)
                            {
                                return $"Заказ #{orderId} не найден";
                            }

                            orderReader.Read();
                            orderDate = orderReader.GetDateTime(1);
                            orderTotal = orderReader.GetInt32(2);
                            orderStatus = orderReader.GetString(3);
                            userEmail = orderReader.GetString(4);
                            orderAddress = orderReader.GetString(5);
                        }
                    }

                    // 2. Получаем детали заказа
                    string detailsQuery = @"
        SELECT 
            g.id,
            g.title,
            oi.amount,
            g.price,
            (oi.amount * g.price) AS item_total
        FROM orderItems oi
        JOIN goods g ON oi.good_id = g.id
        WHERE oi.order_id = @order_id
        ORDER BY g.title";

                    using (SqlCommand detailsCmd = new SqlCommand(detailsQuery, sqlConnection))
                    {
                        detailsCmd.Parameters.AddWithValue("@order_id", orderId);

                        // Формируем заголовок с полной информацией о заказе
                        result.AppendLine("╔════════════════════════════════════════════════════════════╗");
                        result.AppendLine($"║ ЗАКАЗ #{orderId.ToString().PadRight(51)} ║");
                        result.AppendLine($"║ Дата: {orderDate.ToString("yyyy-MM-dd HH:mm").PadRight(52)} ║");
                        result.AppendLine($"║ Статус: {orderStatus.PadRight(50)} ║");
                        result.AppendLine($"║ Почта: {Truncate(userEmail, 45).PadRight(46)} ║");
                        result.AppendLine($"║ Адрес: {Truncate(orderAddress, 45).PadRight(49)} ║");
                        result.AppendLine($"║ Сумма: {orderTotal.ToString().PadLeft(44)} ║");
                        result.AppendLine("╠════════════════════════════════════════════════════════════╣");

                        using (SqlDataReader detailsReader = detailsCmd.ExecuteReader())
                        {
                            if (detailsReader.HasRows)
                            {
                                result.AppendLine("║                       ДЕТАЛИ ЗАКАЗА                   ║");
                                result.AppendLine("╠══════╦══════════════════════╦══════════╦══════════════╗");
                                result.AppendLine("║ ID   ║ Товар                ║ Кол-во   ║ Итого        ║");
                                result.AppendLine("╠══════╬══════════════════════╬══════════╬══════════════╣");

                                while (detailsReader.Read())
                                {
                                    string goodId = detailsReader["id"].ToString();
                                    string title = detailsReader["title"].ToString();
                                    int amount = Convert.ToInt32(detailsReader["amount"]);
                                    decimal total = Convert.ToInt32(detailsReader["item_total"]);

                                    result.AppendLine($"║ {goodId.PadLeft(4)} ║ " +
                                                    $"{Truncate(title, 20).PadRight(20)} ║ " +
                                                    $"{amount.ToString().PadLeft(8)} ║ " +
                                                    $"{total.ToString().PadLeft(12)} ║");
                                }

                                result.AppendLine("╠══════╩══════════════════════╩══════════╩══════════════╣");
                                result.AppendLine($"║ {"ИТОГО:".PadRight(30)} {orderTotal.ToString().PadLeft(22)} ║");
                                result.AppendLine("╚═══════════════════════════════════════════════════════╝");
                            }
                            else
                            {
                                result.AppendLine("║           Товары не найдены                      ║");
                                result.AppendLine("╚══════════════════════════════════════════════════╝");
                            }
                        }
                    }
                }
            }
            catch (FormatException)
            {
                return "Неверный формат идентификатора";
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }

            return result.ToString();
        }
        public string ChangeOrderStatus(string orderId, string newStatus)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверяем валидность good_id
                if (!int.TryParse(orderId, out int orderId1))
                {
                    return "Неверный формат идентификатора";
                }
                if (newStatus != "Processing" && newStatus != "Delivering" && newStatus != "Finished")
                {
                    return "Неверный формат статуса";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        string checkGoodQuery = "SELECT * FROM orders WHERE id = @id";

                        using (SqlCommand checkCmd = new SqlCommand(checkGoodQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@id", orderId1);

                            using (SqlDataReader reader = checkCmd.ExecuteReader())
                            {
                                if (!reader.HasRows)
                                {
                                    reader.Close();
                                    transaction.Rollback();
                                    return "Заказ не найден";
                                }
                                reader.Close();
                                string updateQuery = "UPDATE orders SET status = @newStatus WHERE id = @id";
                                using (SqlCommand updateCmd = new SqlCommand(updateQuery, sqlConnection, transaction))
                                {
                                    updateCmd.Parameters.AddWithValue("@id", orderId1);
                                    updateCmd.Parameters.AddWithValue("@newStatus", newStatus);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return "Статус заказа успешно изменен";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка при изменении статуса: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string AddNewGood(string title, string description, string amount, string price, string category)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                // Проверка валидности входных данных
                if (!int.TryParse(amount, out int amountValue) || amountValue < 0)
                {
                    return "Неверный формат количества";
                }
                if (!int.TryParse(price, out int priceValue) || priceValue <= 0)
                {
                    return "Неверный формат цены";
                }
                if (string.IsNullOrWhiteSpace(title))
                {
                    return "Название на может быть пустым";
                }
                if (string.IsNullOrWhiteSpace(description))
                {
                    return "Описание не может быть пустым";
                }
                if (string.IsNullOrWhiteSpace(category))
                {
                    return "Название категории не может быть пустым";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        int categoryId;

                        // 1. Проверяем существование категории
                        string checkCategoryQuery = "SELECT id FROM categories WHERE title = @title";
                        using (SqlCommand checkCmd = new SqlCommand(checkCategoryQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@title", category);
                            object result = checkCmd.ExecuteScalar();

                            if (result != null)
                            {
                                // Категория существует - используем её ID
                                categoryId = (int)result;
                            }
                            else
                            {
                                // 2. Категории нет - создаём новую
                                string createCategoryQuery =
                                    "INSERT INTO categories (title) VALUES (@title); SELECT SCOPE_IDENTITY();";

                                using (SqlCommand createCmd = new SqlCommand(createCategoryQuery, sqlConnection, transaction))
                                {
                                    createCmd.Parameters.AddWithValue("@title", category);
                                    categoryId = Convert.ToInt32(createCmd.ExecuteScalar());
                                }
                            }
                        }

                        // 3. Добавляем новый товар
                        string insertGoodQuery = @"
                    INSERT INTO goods (title, description, amount, price, category_id) 
                    VALUES (@title, @description, @amount, @price, @category_id);
                    SELECT SCOPE_IDENTITY();";

                        int newGoodId;
                        using (SqlCommand insertCmd = new SqlCommand(insertGoodQuery, sqlConnection, transaction))
                        {
                            insertCmd.Parameters.AddWithValue("@title", title);
                            insertCmd.Parameters.AddWithValue("@description", description);
                            insertCmd.Parameters.AddWithValue("@amount", amountValue);
                            insertCmd.Parameters.AddWithValue("@price", priceValue);
                            insertCmd.Parameters.AddWithValue("@category_id", categoryId);

                            newGoodId = Convert.ToInt32(insertCmd.ExecuteScalar());
                        }

                        transaction.Commit();
                        return $"Товар #{newGoodId} '{title}' успешно добавлен в категорию '{category}'";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка при добавлении товара: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
        public string AddNewCategory(string title)
        {
            string strSqlConnection = "Server=localhost;Database=db_tp_cw;Trusted_Connection=True;";

            try
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    return "Название не может быть пустым";
                }

                using (SqlConnection sqlConnection = new SqlConnection(strSqlConnection))
                {
                    sqlConnection.Open();
                    SqlTransaction transaction = sqlConnection.BeginTransaction();

                    try
                    {
                        int categoryId;

                        // 1. Проверяем существование категории
                        string checkCategoryQuery = "SELECT id FROM categories WHERE title = @title";
                        using (SqlCommand checkCmd = new SqlCommand(checkCategoryQuery, sqlConnection, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@title", title);
                            object result = checkCmd.ExecuteScalar();

                            if (result != null)
                            {
                                // Категория существует - используем её ID
                                transaction.Rollback();
                                return "Категория уже существует";
                            }
                            else
                            {
                                // 2. Категории нет - создаём новую
                                string createCategoryQuery =
                                    "INSERT INTO categories (title) VALUES (@title); SELECT SCOPE_IDENTITY();";

                                using (SqlCommand createCmd = new SqlCommand(createCategoryQuery, sqlConnection, transaction))
                                {
                                    createCmd.Parameters.AddWithValue("@title", title);
                                    categoryId = Convert.ToInt32(createCmd.ExecuteScalar());
                                }
                            }
                        }
                        transaction.Commit();
                        return $"Категория #{categoryId} '{title}' успешно добавлена";
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return $"Ошибка при добавлении категории: {ex.Message}";
                    }
                }
            }
            catch (SqlException ex)
            {
                return $"Ошибка в базе данных: {ex.Message}";
            }
        }
    }
    public class ServerConsole : MarshalByRefObject
    {
        public ServerConsole()
        {
            Console.WriteLine("Создан удаленный объект ServerConsole");
        }
        ~ServerConsole()
        {
            Console.WriteLine("Уничтожен удаленный объект ServerConsole");
        }
        public override Object InitializeLifetimeService()
        {
            return null;
        }
        public void SendConsoleMessage(string message)
        {
            Console.WriteLine(message);
        }
    }

    public class CartItem
    {
        public int GoodId { get; set; }
        public int Price { get; set; }
        public int Quantity { get; set; }
        public int StockAmount { get; set; }
    }

}
