using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ChessConsole
{
    public readonly IEnumerable<IConsoleCommand> chessCommands;
    public ChessConsole(IEnumerable<IConsoleCommand> commands)
    {
        chessCommands = commands;
    }

    public void ParseCommandLine(string commandLine)
    {
        if (commandLine == string.Empty) return;
        string[] inputList = commandLine.Trim().Split(' ');
        string commandToRun = inputList[0];
        string[] args = inputList.Skip(1).ToArray();
        ProcessCommand(commandToRun, args);
    }

    public void ProcessCommand(string calledCommand, string[] args) //actually runs commands
    {
        if (calledCommand == string.Empty) return;
        foreach (var command in chessCommands)
        {
            if(!command.CommandString.Equals(calledCommand, System.StringComparison.OrdinalIgnoreCase)) //not a command
            {
                continue;
            }
            if(command.Action(args))
            {
                return;
            }
        }
    }
}


public class ConsoleBehaviour : MonoBehaviour
{
    [SerializeField] private ConsoleCommand[] consoleCommands;
    [SerializeField] private InputField inputUI;
    [SerializeField] private Text outputUI;
    [SerializeField] private ScrollRect scrollRect;

    ChessConsole console;
    string lastInput = "";
    int lineCount = 0;
    int lineLimit = 700; //prevents Execption from mesh having too many vertices


    private void Start()
    {
        //inputUI.onEndEdit.AddListener(RunCommand);
        console = new ChessConsole(consoleCommands);
        foreach (ConsoleCommand command in consoleCommands)
        {
            command.Init();
        }
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Return)) 
        {
            string[] commandsToRun = inputUI.text.Split(';');
            foreach (string command in commandsToRun)
            {
                RunCommand(command);
            }
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            inputUI.text = lastInput; //for easy repeated inputs
            inputUI.caretPosition = inputUI.text.Length;
        }
    }

    public void RunCommand(string input)
    {
        outputUI.text += "\n> " + input;
        console.ParseCommandLine(input);
        lastInput = input;
        inputUI.text = string.Empty;
        scrollRect.verticalNormalizedPosition = 0;
        if (Application.platform != RuntimePlatform.Android)
        {
            inputUI.ActivateInputField();
        }
    }

    public void Print(string line)
    {
        lineCount += 2;
        if (lineCount >= lineLimit)
        {
            Cutoff(2); //oldest two lines must go to prevent overflow
        }
        outputUI.text += "\n" + line + "\n";
        
    }

    public void Line(string line) {
        lineCount++;
        if (lineCount >= lineLimit)
        {
            Cutoff(1); //oldest line must go to prevent overflow
        }
        outputUI.text += "\n" + line;
    }

    private void Cutoff(int lines) //cuts a certain number of lines from the top of the console
    {
        List<string> splitTxt = outputUI.text.Split('\n').ToList<string>();
        splitTxt.RemoveRange(0, lines);
        outputUI.text = string.Join("\n", splitTxt.ToArray<string>());
        lineCount -= lines;
    }

    public void ReplaceLast(string line)
    {
        var splitText = outputUI.text.Split( '\n');
        splitText[splitText.Length-1] = line;
        outputUI.text = string.Join("\n", splitText);
    }

    public void Clear()
    {
        outputUI.text = "";
    }

    public void Help()
    {
        foreach(ConsoleCommand command in consoleCommands)
        {
            if (command is HelpCommand) continue; //excludes the help command
            Line(command.commandString);
            Print(command.helpString);
        }
    }
}
