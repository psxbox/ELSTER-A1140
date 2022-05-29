using ElsterA1140Reader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.IO.Ports;
using System.CommandLine;
using MQTTnet;
using MQTTnet.Client.Options;
using System.Text.Json;
using System.Text.Json.Nodes;
using MQTTnet.Client;

var dataTypeOption = new Option<string>(
    "--data-type",
    getDefaultValue: () => "current",
    description: "Data type: current - last data"
);

var serialOption = new Option<string>(
    "--serial",
    description: "Serial port (eg. COM1)"
);
serialOption.IsRequired = true;

var baudOption = new Option<int>(
    "--baud-rate",
    getDefaultValue: () => 9600,
    description: "Serial port baudrate"
);

var deviceIdOption = new Option<int>(
    "--id",
    "Device id"
);
deviceIdOption.IsRequired = true;

var deviceNameOption = new Option<string>(
    "--device-name",
    description: "A name of device"
);
deviceNameOption.IsRequired = true;

var deviceTypeOption = new Option<string>(
    "--device-type",
    getDefaultValue: () => "ALFA_A1140",
    description: "Device type, default: ALFA_A1140"
);

var serverOption = new Option<string>(
    "--server",
    getDefaultValue: () => "localhost",
    description: "BSG Scada server dns or ip"
);

var serverPortOption = new Option<int>(
    "--port",
    getDefaultValue: () => 1883,
    description: "BSG Scada mqtt port"
);

var gatewayTokenOption = new Option<string>(
    "--token",
    "BGS Scada gateway device token"
);
gatewayTokenOption.IsRequired = true;

RootCommand rootCommand = new() {
    dataTypeOption,
    serverOption,
    serverPortOption,
    gatewayTokenOption,
    serialOption,
    baudOption,
    deviceIdOption,
    deviceNameOption,
    deviceTypeOption
};

const string DEVICE_CONNECT_TOPIC = "v1/gateway/connect";
const string TELEMETRY_TOPIC = "v1/gateway/telemetry";

string[] dataTypes = { "current", "load_table" };
using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
ILogger logger = loggerFactory.CreateLogger<Program>();
SerialPort serialPort;
A1140Reader a1140Reader;
IMqttClient mqttClient;



rootCommand.Description = "Get data from Alfa A1140 and save to BSG Scada";

rootCommand.SetHandler((
    string server,
    int port,
    string token,
    string serial,
    int baud,
    int deviceId,
    string deviceName,
    string deviceType,
    string dataType) =>
    RootHandle(server, port, token, serial, baud, deviceId, deviceName, deviceType, dataType),
    serverOption,
    serverPortOption,
    gatewayTokenOption,
    serialOption,
    baudOption,
    deviceIdOption,
    deviceNameOption,
    deviceTypeOption,
    dataTypeOption);

return rootCommand.Invoke(args);

async Task<int> RootHandle(
    string server,
    int port,
    string token,
    string serial,
    int baud,
    int deviceId,
    string deviceName,
    string deviceType,
    string dataType)
{
    if (!dataTypes.Any(s => s.Equals(dataType)))
    {
        logger?.LogWarning("data-type sintaksis xatolik");
        return -1;
    }

    if (!ConnectToSerial(serial, baud))
    {
        return -1;
    }

    if (!OpenSession(deviceId))
    {
        return -1;
    }

    DateTime? deviceTime = a1140Reader.GetDeviceTime();
    DateTime localTime = DateTime.Now;
    var timeDiff = localTime - deviceTime;
    logger?.LogInformation("Local time: {d}\tDevice time: {dt}\tFarq: {f}", localTime, deviceName, timeDiff);

    //a1140Reader.ReadLoadTable(2);

    return 0;

    var options = new MqttClientOptionsBuilder()
        .WithTcpServer(server, port)
        .WithCredentials(username: token, password:"")
        .Build();


    mqttClient = new MqttFactory().CreateMqttClient();
    await mqttClient.ConnectAsync(options);

    DeviceInfo deviceInfo = new()
    {
        device = deviceName,
        type = deviceType
    };

    Thread.Sleep(1000);

    //await mqttClient.PublishAsync(topic: DEVICE_CONNECT_TOPIC, payload: JsonSerializer.Serialize(deviceInfo));

    if (dataType.ToLower().Equals("current"))
    {
        var values = a1140Reader.ReadCurrent();
        if (values.Count != 0)
        {
            var doc = new JsonObject();
            var arr = new JsonArray();
            var obj = new JsonObject()
            {
                ["ts"] = (ulong)(DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds,
                ["values"] = JsonSerializer.SerializeToNode(values)
            };
            arr.Add(obj);
            doc[deviceName] = arr;

            var payload = JsonSerializer.Serialize(doc);

            logger?.LogInformation("{a}", payload);
            
            //await mqttClient.PublishAsync(TELEMETRY_TOPIC, payload);
        }
    }


    return 0;
}

bool ConnectToSerial(string serial, int baud)
{
    serialPort = new(serial, baud);
    try
    {
        serialPort.Open();
    }
    catch (Exception e)
    {
        logger.LogError(exception: e, "Port openning error");
        return false;
    }
    return true;
}

bool OpenSession(int deviceId)
{
    a1140Reader = new(serialPort, deviceId, loggerFactory: loggerFactory);
    return a1140Reader.OpenSession();
}

