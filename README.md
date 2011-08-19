Mizu
====

Introduction
------------

Mizu is my attempt at creating a compiler for the .NET framework. It compiles a simple esoteric programming language into a executable that runs on the framework. That means the executables are true .NET executes and are compatible with ones that are generated by **csc** (C# compiler) and **vbc** (VB.NET compiler).

The name '**Mizu**' means water in Japanese and Mizu (the language) flows like water from left to right.

I took inspiration in making this a math-based language from **Fortran**. 

At the moment, the compiler is written in **C#** but when I get the time, I will port it to **VB.NET** as well.

Tools
-----
I used the following tools in my creation of the compiler.

+   ILSpy (by the SharpDevelop team) for viewing the generated IL and viewing the **C#** equalivent.
+   TinyPG for generating the parser/scanner. Search http://codeproject.com for it.
+   PEVerify (included with Visual Studio) for debugging IL problems. (Especially during Invalid CLR program exceptions).

Syntax
------

Since this is my first true programming language and I didn't expect it to have any actual use, the syntax is weird.

	a`5|b`[1..10]|?c:a+b|.c

I'll explain.

The "a`5" bit declares a variable named "**a**" and assigns it the value of "**5**".

The "b`[1..10]" declares a variable named "**b**" and creates a for loop. B will iterate through **[1..9]** (**10** is exclusive).

"?c:a+b" declares a variable of "**c**" as the output of "**a+b**". Remember, **b** is looping variable, so this statement will repeat. Every statement on the right of a loop is in the loop.

".c" prints the value of "**c**".

As of commit [2820921cd45e252db90b24b69c126afba3a219e8](https://github.com/Amrykid/Project-Mizu/commit/2820921cd45e252db90b24b69c126afba3a219e8), Mizu supports multi-line code:

	﻿c`^|a`5	
	b`[1..99]	
	?res=2+(2+3)+(b*a)	
	res	
	.c


Usage
-----

To use Mizu, you would call it from the command line like this:

>   mizu ExampleInput.miz ExampleOutput.exe

If nessesscary, you can append switches onto the end of the above command. Here are the available switches:

+   /debug - Generates debugging information (and symbols).

+   /run - Runs the executable after a successful compilation.

+   /invalid - Generates an invalid executable. (This is basically for my debugging use.)

Technical Stuff
---------------

Instead of evaluating mathmatical expressions in IL, I created a external lib for evaling expressions in JScript.NET (**jsc**). JScript has an internal 'eval' function so I decided to math use of that.


Contact Me
----------

If you have any questions, you can find me on IRC. ##XAMPP @ irc.freenode.net

Remember, theres two #, not one.

Credits
-------

I would like to thank Mike Danes from the MSDN forums for his help with solving my "stack depth" issues. You can see the thread here -> http://social.msdn.microsoft.com/Forums/eu/clr/thread/ad499556-3a2f-4530-9584-2ec9ca76da53