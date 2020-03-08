# Liquid Prompt Win - An adaptation for Windows Cmd

This project has been inspired by https://github.com/nojhan/liquidprompt.
It is written in C# as a Windows Framework Console Application and does currently not support interactive commands and also not the full feature list that liquid prompt does. (also this readme plagiarizes nojhan's)

## Features
* when inside a github repository:
    * will show this current working branch as [master] and optionally the number of stashes behind the branch name, and how far it is behind/ahead of the remote branch
    * Then repository info as follows:
        * \+ (added)
        * ~ (modified)
        * \- (removed)
        * @ (staged)
        * ? (untracked)
        * x (missing)
    * example: C:\Users\JMarianczuk\Repos\LiquidPromptWin [master (~1 | ?1)]>
* Special commands:
    * exit: stops this Liquid Prompt Win instance
    * sudo x: run x with elevated permissions
    * sudocommand x: start a new cmd window with elevated permissions in the same directory as the current working directory and execute x (alias: sudoc)
* General: if a command cannot be executed, LPWin will try to get a cmd instance to execute it. For example, the command "echo hi" will not work on its own, but the command processor knows what to do with it.

## Usage
* build the project in any location
* grab a folder X that has been added to your computer's PATH variable
* create a shortcut to LPWin's executable inside X. I recommend giving it a short name ("LP.LNK" for example)
* Go to the shortcut's properties and set "Start in:" to "%CD%". This will run LP in the directory you called it from (or from the place you called it from in cmd).
* Either run LP directly or from cmd.

## Notice: Since interactive commands are not supported, starting any interactive command from within LP will result in an exception when the command tries to read from the console.