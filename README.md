## About
Ogam3 is a lightweight implementation of Scheme programming language written on C#. The interpreter core has been developed from scratch without any external libs although there are several dependencies for data transfer. Most of the decisions were made to guarantee comfort and ease of use. The interpreter provides wide opportunities to configure and manage .Net applications through REPL (read-eval-print-loop) and has high performance and powerful RPC technology based on S-Expression.

Ogam3 interpreter consists of several parts: the Reader, the Compiler and the Language Virtual Machine. Two implementations of the Reader are available, one for string format and the other for binary format. The string Reader is used for REPL and for loading scripts while the binary Reader is brought mainly to implement client-server exchange protocol. Splitting the interpreter into the compiler and the VM allows further improvements and various optimizations including runtime compilation in the future.

## Features
To convert .NET objects to S-Expressions and vice versa, an efficient serializer has been developed. The serializer compiles dynamically which leads to higher performance and ensures quick field access. Therefore, the very first serialization/deserialization of the objects takes some additional time to generate dynamically compiled methods. And after that, serialization operations are performed very quickly.

To reduce network data traffic, the LZ4 compression algorithm is used. The choice of this algorithm is primary due to its high speed data processing with a minimum processor load.

The network protocol provides duplex communication interface which allows to notify clients about a server state change and to implement a message queue.

The Lisp based data transfer protocol can be considered as a query language. This allows to combine server procedures for obtaining results originally not provided. For example, in case of a service implementing several procedures that return item lists and one procedure that returns the number of items in the list, it is possible to combine these procedures in one query and to get items count without any need to transfer list to client-side. Thus, the costs associated with network data transmission and serialization / deserialization are removed. It is possible to create queries of a higher complexity and to save them on a server using the special form Define for a routine use.

## Why
Our applications require high-speed exchange of structured data, transmission of binary data streams and execution of queries in both directions between Client and Server. Therefore, as an experiment, we developed a network protocol based on S-Expression called Ogam. At the moment, we have reached version 3 and want to share the development results.

Programs and data of Lisp are described using S-Expression. Unified format for presenting data and code allows one to develop a simple and efficient parser. In addition, S-Expression is quite compact, allows a developer to easily mix code and data in a query to a server and to reduce network traffic. Scheme was chosen because it is minimalistic but powerful enough to implement complex program logic if needed.
## Examples
First, you should download and build the Ogam3 solution. Then add Ogam3.dll references to your project and put “using Ogam3” namespace.

Simple calls

To evaluate a string you can write:
```csharp
var result = "(+ 1 2 3)".O3Eval();
```
To call C# methods from Ogam you can extend interpreter:
```csharp
"say-sello".O3Extend(new Action<string>(name => {
    Console.WriteLine($"Hello {name}");
}));
```
After that, just call it:
```csharp
"(say-hello \"Bob\")".O3Eval();
```
Also, you can use Function<> for return value;

You can even specify Evaluator:
```csharp
var evl = new Evaluator();
var result = evl.EvlString("(+ 1 2 3)");
var result2 = evl.EvlSeq(Cons.List("+".O3Symbol(), 1, 2, 3)); // Without reader
```
The last string shows how to use evaluator without reader. It is helpful when you need to push .net object into evaluator.

Ogam client-server
First, you need to specify a communication interface. To do this, you create a common assembly and specify Interface:
```csharp
[Enviroment ("server-side")]
public interface IServerSide {
    int IntSumm(int a, int b);
    double DoubleSumm(double a, double b);
    void WriteMessage(string text);
    void NotImplemented();
    ExampleDTO TestSerializer(ExampleDTO dto);
    void Subscribe();
}
```
The interface should be marked with environment attribute `[Enviroment ("server-side")]`.
To demonstrate Ogam3 serializer you can create a data transfer class and put it into common assembly:
```csharp
public class ExampleDTO {
    public string StringValue;
    public DateTime DateTimeValue;
    public int IntegerValue;
    public double DoubleValue;
    public MemoryStream StreamValue;
    public List<int> IntList;
}
```
Then you can register interface implementation and start **server**:
```csharp
LogTextWriter.InitLogMode();
var srv = new OTcpServer(1010);
var impl = new ServerLogigImplementation();
srv.RegisterImplementation(impl);
Console.ReadLine();
```
Now on **client-side**:
```csharp
// Create connection
var cli = new OTcpClient("localhost", 1010);

// Server error handler
cli.SpecialMessageEvt += (message, o) => {
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($">> {o}");
    Console.WriteLine($"<< {message}");
    Console.ResetColor();
};

//Create transfer interface
var pc = cli.CreateProxy<IServerSide>();

//Call server
Console.WriteLine($"pc.IntSumm(11, 33) = {pc.IntSumm(11, 33)}");
Console.WriteLine($"pc.IntSummOfPower(11, 33) = {pc.IntSummOfPower(11, 33)}");

pc.WriteMessage("Hello server!");

pc.NotImplemented();

var dto = new ExampleDTO {
    DateTimeValue = DateTime.Now,
    DoubleValue = 11.33,
    IntegerValue = 1133,
    StringValue = "String message",
    IntList = new int[100].ToList(),
    StreamValue = new MemoryStream(new byte[5000])
};

var echoDto = pc.TestSerializer(dto);
```

## Future
Now Ogam3 is ready for everyday use. However, there are many more ideas that we plan to implement.

 - Modify the binary S-Expression to optimize data presentation.
 - Use streaming compression lz4 for the data exchange session. Now packages are compressed separately, so total compression is not as effective as it can be.
 - Implement data transferring through named pipes for efficient data traffic within a single computer.
   * Add support for macros.
   * Fully implement the standard RnRs
 - Make .net compiler for scheme.

## Contributing
Any contributions and suggestions are welcome, feel free to contact us and to take part in the development of the project.

## License
Copyright 2018 Tinkoff Bank
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

