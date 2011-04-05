ZExtensions
===========

Some useful C# classes and extensions for application development

Examples
--------
* Command line arguments parser

	void Main(string[] args)
	{
		var parser = CommandLineArgumentsParser();
		var helpArgument = new Argument<bool>("show this menu");
		var someArgument = new Argument<string>("some other really importaint argument", true);		
        parser.AddArgument(helpArgument, "-h", "--help");
		parser.AddArgument(someArgument, "-s");
		try
		{
			parser.Parse(e.Args);			
			Console.WriteLine("Some argument is: {0}, someArgument.Value);
        }
        catch (Exception exception)
        {
			Console.WriteLine(exception);
			Console.WriteLine(parser.GetHelpString());
			return;        
		}
		
		if (helpArgument.IsSet)
		{
			Console.WriteLine(parser.GetHelpString());
		}		
	}
