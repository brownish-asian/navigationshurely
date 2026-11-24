using navigationshurely.models;

namespace navigationshurely.services;


public class Processor
{
    private string _response;
    public event Action? ExitTerminal;

    public string Response()
    {
        return _response;
    }

    public void ProcessCommand(List<Token> pieceWiseCommand)
    {
        //ignoring the EOF token at the end of every command
        int actualLength = pieceWiseCommand.Count - 1;

        if (actualLength == 0) 
        {
            MakeResponse("");
            return; 
        }
        if (actualLength == 1)
        {
            Token singularCommand = pieceWiseCommand[0];
            switch (singularCommand.Type)
            {
                case (TokenType.Word):
                    switch (singularCommand.Value)
                    {
                        case ("exit"):
                            ExitTerminal?.Invoke();
                            break;
                        default:
                            break;
                    }
                    break;
            }
        }
    }

    private void MakeResponse(string response)
    {
        _response = response;
    }


}
