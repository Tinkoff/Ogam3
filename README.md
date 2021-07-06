## About
Ogam3 is a lightweight implementation of Scheme like programming language written on C#. The interpreter core has been developed from scratch without any external libs. Most of the decisions were made to guarantee comfort and ease of use. The interpreter provides wide opportunities to configure and manage .Net applications through REPL (read-eval-print-loop) and has high performance and powerful RPC technology based on S-Expression.

Ogam3 interpreter consists of several parts: the Reader, the Compiler and the Language Virtual Machine. Two implementations of the Reader are available, one for string format and the other for binary format. The string Reader is used for REPL and for loading scripts while the binary Reader is brought mainly to implement client-server exchange protocol. Splitting the interpreter into the compiler and the VM allows further improvements and various optimizations including runtime compilation in the future.

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

