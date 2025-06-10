using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class PlayerStats
{
    public string Name { get; set; }
    public int Wins { get; set; }
    public int GamesPlayed { get; set; }
}

public class GameHistory
{
    public List<PlayerStats> Players { get; set; } = new List<PlayerStats>();
}

public class ConsoleOutputService
{
    private readonly Dictionary<string, Dictionary<string, string>> _languageDictionary;
    private string _currentLanguage;

    public ConsoleOutputService(Dictionary<string, Dictionary<string, string>> languageDictionary, string currentLanguage)
    {
        _languageDictionary = languageDictionary;
        _currentLanguage = currentLanguage;
    }

    public void SetLanguage(string language) => _currentLanguage = language;

    public void DisplayMessage(string messageKey, params object[] args)
    {
        string message = GetLocalizedMessage(messageKey);
        Console.WriteLine(args.Length > 0 ? string.Format(message, args) : message);
    }

    public void DisplayPrompt(string messageKey, params object[] args)
    {
        string message = GetLocalizedMessage(messageKey);
        Console.Write(args.Length > 0 ? string.Format(message, args) : message);
    }

    public string GetLocalizedMessage(string messageKey) =>
        _languageDictionary[_currentLanguage].TryGetValue(messageKey, out var message) ? message : messageKey;

    public void DisplayUsedWords(IEnumerable<string> usedWords) =>
        Console.WriteLine(GetLocalizedMessage("UsedWords") + string.Join(", ", usedWords));
}

public class ConsoleInputService
{
    private readonly ConsoleOutputService _output;

    public ConsoleInputService(ConsoleOutputService outputService) => _output = outputService;

    public string GetUserInput()
    {
        string input = Console.ReadLine();

        while (string.IsNullOrEmpty(input))
        {
            input = Console.ReadLine();
        }
        return input.Trim();
    }

    public int GetNumberInput(int minValue, int maxValue)
    {
        while (true)
        {
            if (int.TryParse(Console.ReadLine(), out int result) && result >= minValue && result <= maxValue)
                return result;
            _output.DisplayMessage("InvalidNumber");
        }
    }

    public string GetPlayerName(string playerNumber)
    {
        _output.DisplayPrompt("EnterPlayerName", playerNumber);
        return GetUserInput();
    }

    public string GetLanguageSelection()
    {
        while (true)
        {
            _output.DisplayPrompt("SelectLanguage");
            string input = GetUserInput();
            if (input == "1" || input == "2") return input;
            _output.DisplayMessage("InvalidLanguage");
        }
    }

    public string GetInitialWord(int minLength, int maxLength)
    {
        while (true)
        {
            _output.DisplayPrompt("EnterInitialWord");
            string word = GetUserInput()?.ToLower();

            if (string.IsNullOrEmpty(word)) continue;

            if (word.Length < minLength || word.Length > maxLength)
            {
                _output.DisplayMessage("WordLengthError");
                continue;
            }

            if (!word.All(char.IsLetter))
            {
                _output.DisplayMessage("LettersOnly");
                continue;
            }

            return word;
        }
    }
}

public class WordGame
{
    private string _originalWord;
    private Dictionary<char, int> _originalLetters;
    private Queue<string> _players;
    private List<string> _usedWords;
    private Timer _timer;
    private bool _isTimeUp;
    private int _timeLimitInSeconds;
    private readonly ConsoleInputService _inputService;
    private readonly ConsoleOutputService _output;
    private Dictionary<string, PlayerStats> _playerStats;
    private string StatsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game_stats.json");

    public WordGame(ConsoleInputService inputService, ConsoleOutputService outputService)
    {
        _inputService = inputService;
        _output = outputService;
        _usedWords = new List<string>();
        _timeLimitInSeconds = 5;
        LoadStats();
    }

    private void LoadStats()
    {
        try
        {
            if (File.Exists(StatsFilePath))  
            {
                var json = File.ReadAllText(StatsFilePath);
                var history = JsonSerializer.Deserialize<GameHistory>(json);
                _playerStats = history.Players.ToDictionary(p => p.Name);
            }
            else
            {
                _playerStats = new Dictionary<string, PlayerStats>();
            }
        }
        catch
        {
            _playerStats = new Dictionary<string, PlayerStats>();
        }
    }

    private void SaveStats(string winnerName)
{
    try
    {
        var existingStats = new GameHistory { Players = new List<PlayerStats>() };

        if (File.Exists(StatsFilePath))
        {
            var json = File.ReadAllText(StatsFilePath);
            existingStats = JsonSerializer.Deserialize<GameHistory>(json) ?? existingStats;
        }

        foreach (var player in _players)
        {
            var playerStat = existingStats.Players.FirstOrDefault(p => p.Name == player);

            if (playerStat == null)
            {
                playerStat = new PlayerStats { Name = player };
                existingStats.Players.Add(playerStat);
            }

            playerStat.GamesPlayed++;

            if (player == winnerName)
            {
                playerStat.Wins++;
            }
        }

        if (!_players.Contains(winnerName))
        {
            var winnerStat = existingStats.Players.FirstOrDefault(p => p.Name == winnerName);
            if (winnerStat == null)
            {
                winnerStat = new PlayerStats { Name = winnerName };
                existingStats.Players.Add(winnerStat);
            }
            winnerStat.Wins++;
            winnerStat.GamesPlayed++;
        }

        var updatedJson = JsonSerializer.Serialize(existingStats, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StatsFilePath, updatedJson);
    }
    catch (Exception ex)
    {
            _output.DisplayMessage("StatsSaveError", ex.Message);
    }
}

    public void InitializePlayers()
    {
        _players = new Queue<string>();
        for (int i = 1; i <= 2; i++)
        {
            string playerName;
            do
            {
                playerName = _inputService.GetPlayerName(i.ToString());
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    _output.DisplayMessage("InvalidPlayerName");
                }
            } while (string.IsNullOrWhiteSpace(playerName));
            _players.Enqueue(playerName);
        }
    }

    public void SelectLanguage()
    {
        string input = _inputService.GetLanguageSelection();
        _output.SetLanguage(input == "1" ? "ru" : "en");
    }

    public void InitializeGame()
    {
        _originalWord = _inputService.GetInitialWord(8, 30);
        _originalLetters = _originalWord.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

        _output.DisplayPrompt("SetTimeLimit");
        _timeLimitInSeconds = _inputService.GetNumberInput(5, 60);
    }
    public bool PlayRound()
    {
        _output.DisplayMessage("GameStarted");
        _output.DisplayMessage("InitialWord", _originalWord);
        Console.WriteLine("---------------------------------------------------");

        while (true)
        {
            string currentPlayer = _players.Peek();

            _output.DisplayMessage("PlayerTurn", currentPlayer);

            Console.WriteLine("\nДоступные команды: / Available commands:");
            Console.WriteLine("/show-words");
            Console.WriteLine("/score");
            Console.WriteLine("/total-score");

            string inputWord = GetPlayerInput(currentPlayer);

            if (inputWord == null)
            {
                _players.Enqueue(_players.Dequeue());
                string winner = _players.Dequeue();
                _output.DisplayMessage("PlayerLost", currentPlayer, _timeLimitInSeconds);
                _output.DisplayMessage("Winner", winner);
                SaveStats(winner);
                return true;
            }

            if (inputWord.StartsWith("/"))
            {
                ProcessCommand(inputWord);
                continue;
            }

            _usedWords.Add(inputWord);
            _players.Enqueue(_players.Dequeue());
        }
    }

    private void ProcessCommand(string command)
    {
        switch (command.ToLower())
        {
            case "/show-words":
                _output.DisplayMessage("CommandShowWords");
                _output.DisplayUsedWords(_usedWords);
                break;

            case "/score":
                _output.DisplayMessage("CommandScore");
                foreach (var player in _players)
                {
                    if (_playerStats.TryGetValue(player, out var stats))
                    {
                        Console.WriteLine($"{player}: {stats.Wins} wins out of {stats.GamesPlayed} games");
                    }
                    else
                    {
                        Console.WriteLine($"{player}: ...");
                    }
                }
                break;

            case "/total-score":
                _output.DisplayMessage("CommandTotalScore");
                foreach (var stat in _playerStats.Values.OrderByDescending(s => s.Wins))
                {
                    Console.WriteLine($"{stat.Name}: {stat.Wins} wins out of {stat.GamesPlayed} games");
                }
                break;

            default:
                _output.DisplayMessage("UnknownCommand");
                break;
        }
    }


    private string GetPlayerInput(string playerName)
    {
        while (Console.KeyAvailable)
        Console.ReadKey(true);
        while (true)
        {
            try
            {
                StartTimer();
                _output.DisplayPrompt("EnterWord");
                string input = null;
                _isTimeUp = false;

                var inputTask = Task.Run(() => input = _inputService.GetUserInput()?.ToLower());

                while (!inputTask.IsCompleted)
                {
                    if (_isTimeUp)
                    {
                        _output.DisplayMessage("TimeOut");
                        return null;
                    }
                    Thread.Sleep(100);
                }

                _timer?.Dispose();

                if (string.IsNullOrEmpty(input))
                {
                    _output.DisplayMessage("EmptyInput");
                    continue;
                }

                if (input.StartsWith("/")) return input;
                if (input == _originalWord) throw new Exception(_output.GetLocalizedMessage("OriginalWordError"));
                if (_usedWords.Contains(input)) throw new Exception(_output.GetLocalizedMessage("WordUsed"));
                if (!IsWordValid(input)) throw new Exception(_output.GetLocalizedMessage("InvalidWord"));

                return input;
            }
            catch (Exception ex)
            {
                _output.DisplayMessage(ex.Message);
            }
        }
    }

    private bool IsWordValid(string word)
    {
        var wordLetter = word.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        return wordLetter.All(letter =>
            _originalLetters.ContainsKey(letter.Key) &&
            _originalLetters[letter.Key] >= letter.Value);
    }

    private void StartTimer()
    {
        _isTimeUp = false;
        _timer = new Timer(_ => _isTimeUp = true, null, _timeLimitInSeconds * 1000, Timeout.Infinite);
    }
}

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var languageDictionary = InitializeLanguageDictionary();
            var outputService = new ConsoleOutputService(languageDictionary, "ru");
            var inputService = new ConsoleInputService(outputService);
            var game = new WordGame(inputService, outputService);

            outputService.DisplayMessage("Rules");
            game.SelectLanguage();
            game.InitializePlayers();
            game.InitializeGame();
            game.PlayRound();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
        }
    }

    static Dictionary<string, Dictionary<string, string>> InitializeLanguageDictionary()
    {
        var languageDictionary = new Dictionary<string, Dictionary<string, string>>();

        languageDictionary["ru"] = new Dictionary<string, string>
        {
            {"Rules", "Правила: игроки по очереди вводят слова из букв начального слова.\nПроигрывает тот, кто не может ввести слово за отведенное время."},
            {"EnterPlayerName", "Введите имя игрока {0}: "},
            {"InvalidPlayerName", "Имя игрока не может быть пустым!"},
            {"UnexpectedError", "Произошла непредвиденная ошибка: "},
            {"GameOver", "Игра завершена!"},
            {"EnterInitialWord", "Введите начальное слово (8-30 символов): "},
            {"WordLengthError", "Слово должно быть от 8 до 30 символов!"},
            {"LettersOnly", "Слово должно содержать только буквы!"},
            {"SetTimeLimit", "Установите лимит времени на ход (5-60 секунд): "},
            {"InvalidNumber", "Некорректный ввод. Введите число от 5 до 60."},
            {"GameStarted", "\nИгра началась!"},
            {"InitialWord", "Начальное слово: {0}"},
            {"PlayerTurn", "\nХод игрока: {0}"},
            {"UsedWords", "Использованные слова: "},
            {"EnterWord", "Введите слово: "},
            {"TimeOut", "\nВремя вышло!"},
            {"EmptyInput", "Пустой ввод не допускается!"},
            {"OriginalWordError", "Нельзя использовать начальное слово!"},
            {"WordUsed", "Это слово уже использовалось!"},
            {"InvalidWord", "Неверное слово! Используйте только буквы из начального слова."},
            {"PlayerLost", "\n{0} не успел ввести слово за {1} секунд и проиграл!"},
            {"Winner", "Победитель: {0}!"},
            {"SelectLanguage", "Выберите язык\n1. Русский\n2. English\nВведите 1 или 2: "},
            {"InvalidLanguage", "Некорректный выбор. Пожалуйста, введите 1 или 2."},
            {"CommandShowWords", "Все введенные слова в текущей игре:"},
            {"CommandScore", "Текущие результаты игроков:"},
            {"CommandTotalScore", "Общие результаты всех игроков:"},
            {"UnknownCommand", "Неизвестная команда!"}
        };

        languageDictionary["en"] = new Dictionary<string, string>
        {
            {"Rules", "Rules: players take turns entering words made from the letters of the starting word.\nThe player who fails to enter a word within the time limit loses."},
            {"EnterPlayerName", "Enter player {0} name: "},
            {"InvalidPlayerName", "Player name cannot be empty!"},
            {"UnexpectedError", "An unexpected error occurred: "},
            {"GameOver", "Game over!"},
            {"EnterInitialWord", "Enter the starting word (8-30 characters): "},
            {"WordLengthError", "The word must be between 8 and 30 characters long!"},
            {"LettersOnly", "The word must contain only letters!"},
            {"SetTimeLimit", "Set time limit per turn (5-60 seconds): "},
            {"InvalidNumber", "Invalid input. Please enter a number between 5 and 60."},
            {"GameStarted", "\nGame started!"},
            {"InitialWord", "Starting word: {0}"},
            {"PlayerTurn", "\nPlayer's turn: {0}"},
            {"UsedWords", "Used words: "},
            {"EnterWord", "Enter a word: "},
            {"TimeOut", "\nTime's up!"},
            {"EmptyInput", "Empty input is not allowed!"},
            {"OriginalWordError", "You can't use the starting word!"},
            {"WordUsed", "This word has already been used!"},
            {"InvalidWord", "Invalid word! Use only letters from the starting word."},
            {"PlayerLost", "\n{0} failed to enter a word in {1} seconds and loses!"},
            {"Winner", "Winner: {0}!"},
            {"SelectLanguage", "Select language:\n1. Русский\n2. English\nEnter 1 or 2 "},
            {"InvalidLanguage", "Invalid choice. Please enter 1 or 2."},
            {"CommandShowWords", "All words entered in current game:"},
            {"CommandScore", "Current players score:"},
            {"CommandTotalScore", "Total score for all players:"},
            {"UnknownCommand", "Unknown command!"}
        };
        return languageDictionary;
    }
}