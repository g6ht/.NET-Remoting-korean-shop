using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using System.Text;
using System.Threading.Tasks;
using RemoteObjects;
using System.Collections;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Remoting.Channels.Http;
using System.Net.Security;
using System.Net.Sockets;

namespace Server
{
    internal class server
    {
        static void Main(string[] args)
        {
            // Способ 1: Программная конфигурация
            ConfigureRemotingProgrammatically();

            // Способ 2: Конфигурация через файл
            //RemotingConfiguration.Configure("Server.exe.config", false);

            foreach (IChannel channel in ChannelServices.RegisteredChannels)
            {
                Console.WriteLine($"Название канала: {channel.ChannelName}");
                Console.WriteLine($"Приоритет: {channel.ChannelPriority}");

                if (channel is IChannelReceiver receiver)
                {
                    Console.WriteLine($"URL: {receiver.GetUrlsForUri("")[0]}");
                }

                Console.WriteLine(new string('-', 50));
            }

            Console.WriteLine("Нажмите Enter чтобы остановить сервер...\n");
            Console.ReadLine();
        }

        static void ConfigureRemotingProgrammatically()
        {
            BinaryServerFormatterSinkProvider srvPrvdTCP = new BinaryServerFormatterSinkProvider();
            srvPrvdTCP.TypeFilterLevel = TypeFilterLevel.Full;
            BinaryClientFormatterSinkProvider clntPrvdTCP = new BinaryClientFormatterSinkProvider();
            Dictionary<string, string> proprtTCP = new Dictionary<string, string>();
            proprtTCP["port"] = "8086";
            proprtTCP["secure"] = "true";
            proprtTCP["encryptionAlgorithm"] = "AES";
            proprtTCP["protectionLevel"] = "EncryptAndSign";
            TcpChannel channelTCP = new TcpChannel(proprtTCP, clntPrvdTCP, srvPrvdTCP);
            ChannelServices.RegisterChannel(channelTCP, false);

            BinaryServerFormatterSinkProvider srvPrvdHTTP = new BinaryServerFormatterSinkProvider();
            srvPrvdHTTP.TypeFilterLevel = TypeFilterLevel.Full;
            BinaryClientFormatterSinkProvider clntPrvdHTTP = new BinaryClientFormatterSinkProvider();
            Dictionary<string, string> proprtHTTP = new Dictionary<string, string>();
            proprtHTTP["port"] = "8087";
            proprtHTTP["secure"] = "true";
            proprtHTTP["useSsl"] = "true";
            proprtHTTP["protectionLevel"] = "EncryptAndSign";
            HttpChannel channelHTTP = new HttpChannel(proprtHTTP, clntPrvdHTTP, srvPrvdHTTP);
            ChannelServices.RegisterChannel(channelHTTP, false);

            RemotingConfiguration.ApplicationName = "Server";
            RemotingConfiguration.RegisterActivatedServiceType(typeof(User));
            RemotingConfiguration.RegisterActivatedServiceType(typeof(Goods));
            RemotingConfiguration.RegisterActivatedServiceType(typeof(GoodsAdm));
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(ServerConsole),
                                                                    "ServerConsoleURI",
                                                               WellKnownObjectMode.Singleton);
        }
    }
}
