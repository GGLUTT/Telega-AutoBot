using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    private static TelegramBotClient? Bot;
    private static readonly HttpClient httpClient = new HttpClient();
    private static string ApiKey = "Ylddo8PGKJAQmbNBgSq6yrsBdy2trdnzQKkIafdD";

    public static async Task Main(string[] args)
    {
        Bot = new TelegramBotClient("7169948565:AAFZHLP0ZAxWjlw121Bpm51zgAWA760qrCI");

        using var cts = new CancellationTokenSource();

        ReceiverOptions receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = { } // отримувати всі типи оновлень
        };
        Bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken: cts.Token);

        Console.WriteLine($"Запуск бота @{(await Bot.GetMeAsync()).Username}");
        Console.ReadLine();

        cts.Cancel();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message!.Type == MessageType.Text)
        {
            var message = update.Message;
            switch (message.Text.ToLower())
            {
                case "/start":
                    await SendWelcomeMessageAsync(message);
                    break;
                case "легкові марки авто":
                    await SendCarMarksAsync(message.Chat.Id);
                    break;
                case "типи транспорту":
                    await SendCarCategoriesAsync(message.Chat.Id);
                    break;
                case "пошук нових авто":
                    await Bot.SendTextMessageAsync(message.Chat.Id, "Надішліть діапазон цін у форматі 'від ... до ...'. Наприклад: '1500 25000'.");
                    break;
                default:
                    if (message.Text.ToLower().StartsWith("/search"))
                    {
                        await SearchNewCarsAsync(message);
                    }
                    else
                    {
                        await HandleCarMarkSelectionAsync(message);
                    }
                    break;
            }
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            var callbackQuery = update.CallbackQuery;

        }
    }

    private static async Task SendWelcomeMessageAsync(Message message)
    {
        var username = message.Chat.Username;
        var replyKeyboard = new ReplyKeyboardMarkup(new[]
        {
        new KeyboardButton("Легкові марки авто"),
        new KeyboardButton("Типи транспорту"),
        new KeyboardButton("Пошук нових авто"),
        new KeyboardButton("Повернутись до головного меню")
    });

        replyKeyboard.ResizeKeyboard = true;

        await Bot.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: $"Привіт, {username}! Виберіть, що вас цікавить:",
            replyMarkup: replyKeyboard
        );
    }



    private static async Task SendCarMarksAsync(long chatId)
    {
        try
        {
            string url = $"https://developers.ria.com/auto/new/marks?category_id=1&api_key={ApiKey}";
            HttpResponseMessage response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            var carMarks = JsonSerializer.Deserialize<List<CarMarkModel>>(responseBody) ?? new List<CarMarkModel>();

            if (carMarks.Count > 0)
            {
                var buttons = carMarks.Select(m => new KeyboardButton(m.name)).ToArray();
                var replyKeyboard = new ReplyKeyboardMarkup(buttons);

                await Bot.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Оберіть марку автомобіля:",
                    replyMarkup: replyKeyboard
                );
            }
            else
            {
                await Bot.SendTextMessageAsync(chatId, "На жаль, не вдалося отримати список легкових марок авто.");
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("\nException Caught!");
            Console.WriteLine("Message :{0} ", e.Message);
            await Bot.SendTextMessageAsync(chatId, "Виникла помилка при спробі отримати дані з API Auto RIA.");
        }
    }

    private static async Task SendCarCategoriesAsync(long chatId)
    {
        try
        {
            string url = $"https://developers.ria.com/auto/new/categories?api_key={ApiKey}";
            HttpResponseMessage response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            var carCategories = JsonSerializer.Deserialize<List<CarCategoryModel>>(responseBody) ?? new List<CarCategoryModel>();

            if (carCategories.Count > 0)
            {
                var buttons = carCategories.Select(c => new KeyboardButton($"{c.name} ({c.singular})")).ToArray();
                var replyKeyboard = new ReplyKeyboardMarkup(buttons);

                await Bot.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Оберіть тип транспорту:",
                    replyMarkup: replyKeyboard
                );
            }
            else
            {
                await Bot.SendTextMessageAsync(chatId, "На жаль, не вдалося отримати список типів транспорту.");
            }
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("\nException Caught!");
            Console.WriteLine("Message :{0} ", e.Message);
            await Bot.SendTextMessageAsync(chatId, "Виникла помилка при спробі отримати дані з API Auto RIA.");
        }
    }

    private static async Task SearchNewCarsAsync(Message message)
    {
        // Отримуємо текст повідомлення від користувача
        string text = message.Text.ToLower();

        // Розбиваємо текст на окремі слова
        string[] words = text.Split(' ');

        // Шукаємо числа в повідомленні
        List<int> prices = new List<int>();
        foreach (string word in words)
        {
            if (int.TryParse(word, out int price))
            {
                prices.Add(price);
            }
        }

        // Перевіряємо, чи знайдено два числа (від і до)
        if (prices.Count == 2)
        {
            // Формуємо параметри для пошуку
            int priceFrom = Math.Min(prices[0], prices[1]);
            int priceTo = Math.Max(prices[0], prices[1]);

            // Викликаємо метод пошуку автомобілів з вказаним діапазоном цін
            Dictionary<string, string> searchParams = new Dictionary<string, string>
            {
                { "priceFrom", priceFrom.ToString() },
                { "priceTo", priceTo.ToString() }
            };

            string searchResult = await SearchCarsAsync(searchParams);

            // Надсилаємо результат пошуку користувачеві
            await Bot.SendTextMessageAsync(message.Chat.Id, searchResult);
        }
        else
        {
            // Якщо не вдалося знайти два числа, сповіщаємо користувача про помилку вводу
            await Bot.SendTextMessageAsync(message.Chat.Id, "Невірний формат введення. Будь ласка, введіть діапазон цін у форматі 'від ... до ...'. Наприклад: '1500 25000'.");
        }
    }

    private static async Task<string> SearchCarsAsync(Dictionary<string, string> searchParams)
    {
        
        string url = $"https://developers.ria.com/auto/new/search?api_key={ApiKey}";

        // Додаємо параметри пошуку до URL
        foreach (var param in searchParams)
        {
            url += $"&{param.Key}={param.Value}";
        }

        try
        {
            // Відправляємо запит на сервер
            HttpResponseMessage response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Отримуємо результат запиту
            string responseBody = await response.Content.ReadAsStringAsync();

            // Повертаємо результат пошуку у вигляді текстового рядка
            return responseBody;
        }
        catch (HttpRequestException e)
        {
            Console.WriteLine("\nException Caught!");
            Console.WriteLine("Message :{0} ", e.Message);
            return "Виникла помилка при спробі здійснити пошук автомобілів.";
        }
    }

    private static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {

    }

    private static async Task HandleCarMarkSelectionAsync(Message message)
    {

    }
}

public class CarMarkModel
{
    public int mark_id { get; set; }
    public string name { get; set; }

}

public class CarCategoryModel
{
    public string ablative { get; set; }
    public int category_id { get; set; }
    public string name { get; set; }
    public string plural { get; set; }
    public string rewrite { get; set; }
    public string singular { get; set; }
}
