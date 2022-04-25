using ElsterA1140Reader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.IO.Ports;

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger logger = loggerFactory.CreateLogger<Program>();

SerialPort serialPort = new("COM8", 9600);
try
{
    serialPort.Open();
}
catch (Exception e)
{
    logger.LogError(exception: e, "Port openning error");
    return 1;
}


A1140Reader a1140Reader = new(serialPort, 5, loggerFactory: loggerFactory);
a1140Reader.OpenSession();

return 0;
