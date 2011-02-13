/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DemoEventProvider;
using Microsoft.DevLabs.Reactive.Bridge.Instrumentation;
using System.Management;

namespace Demo
{
    class Program
    {
        static void Main()
        {
            //
            // WARNING: You may get an access denied error. In that case, run as an administrator.
            //
            try
            {
                QueryPattern();
                Qbservable();
            }
            catch (ManagementException ex)
            {
                Console.WriteLine("Oops! " + ex.Message + " (Did you read the readme.txt file?)");
                Console.WriteLine();
            }

            //
            // WARNING: In order for this to work, run installutil.exe on DemoEventProvider.dll.
            //
            try
            {
                CustomEvents();
            }
            catch (ManagementException ex)
            {
                Console.WriteLine("Oops! " + ex.Message + " (Did you read the readme.txt file?)");
                Console.WriteLine();
            }
        }

        static void QueryPattern()
        {
            Run(
                "LINQ to WQL - Query pattern",
                () =>
                    (from proc in EventProvider.Query<ProcessStartTrace>(logger: Console.Out)
                     where proc.ProcessName == "notepad.exe"
                     select new { Name = proc.ProcessName, ID = proc.ProcessID })
                    .AsObservable()
                    .Timestamp(),
                () => StartProcessCreation("notepad.exe", 5, 2000)
            );
        }

        static void Qbservable()
        {
            Run(
                "LINQ to WQL - Qbservable",
                () =>
                    (from proc in EventProvider.Qbserve<ProcessStartTrace>(logger: Console.Out)
                     where proc.ProcessName == "notepad.exe"
                     select new { Name = proc.ProcessName, ID = proc.ProcessID })
                    .AsObservable()
                    .Timestamp(),
                () => StartProcessCreation("notepad.exe", 5, 2000)
            );
        }

        static void CustomEvents()
        {
            Run(
                "LINQ to WQL - Custom Events",
                () =>
                    Observable.Return(new StopEvent()).Delay(TimeSpan.FromSeconds(10)).Publish(stop =>
                    {
                        (from tick in Observable.Timer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
                         select new TickEvent { Ticks = tick })
                        .TakeUntil(stop)
                        .Submit();

                        stop.Submit();

                        return EventProvider.Receive<TickEvent>()
                               .TakeUntil(EventProvider.Receive<StopEvent>());
                    }),
                () => {}
            );
        }

        static void Run<T>(string title, Func<IObservable<T>> f, Action a)
        {
            PrintTitle(title);

            using (f().Subscribe(t => Console.WriteLine(t), () => Console.WriteLine("Completed")))
            {
                a();
                Console.ReadLine();
                Console.WriteLine();
            }
        }

        static void PrintTitle(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(title);
            Console.WriteLine(new string('-', title.Length));
            Console.WriteLine();
            Console.ResetColor();
        }

        static void StartProcessCreation(string process, int count, int delay)
        {
            new Thread(() =>
            {
                var procs = EnumerableEx.Repeat(process, count).Select(proc =>
                {
                    Thread.Sleep(delay);
                    return Process.Start(proc);
                }).ToArray();

                Thread.Sleep(delay);
                procs.Run(proc => proc.Kill());
            }).Start();
        }
    }

    [WmiEventClass("Win32_ProcessStartTrace")]
    class ProcessStartTrace
    {
        public uint ProcessID { get; set; }
        public string ProcessName { get; set; }
    }
}
