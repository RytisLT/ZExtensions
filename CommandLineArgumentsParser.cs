using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ZExtensions
{
    public class CommandLineArgumentsParser
    {
        private readonly Dictionary<string, IArgument> argumentKeysDictionary = new Dictionary<string, IArgument>();

        public void AddArgument(IArgument argument, params string[] keys)
        {
            if (argumentKeysDictionary.Values.Any(a => a == argument))
            {
                throw new ApplicationException(string.Format("Argument '{0}' already added", argument.Description));
            }

            foreach (var keyTemp in keys.Select(key => key.Trim()))
            {
                if (!keyTemp.StartsWith("-"))
                {
                    throw new ApplicationException("Key must start with -");
                }
                if (argumentKeysDictionary.ContainsKey(keyTemp))
                {
                    throw new ApplicationException(string.Format("Key {0} was already added", keyTemp));
                }
                argumentKeysDictionary.Add(keyTemp, argument);
            }
        }

        public void Parse(string[] arguments)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                var key = arguments[i].Trim();
                if (!argumentKeysDictionary.ContainsKey(key))
                {
                    throw new ApplicationException(string.Format("Unknown argument {0}", key));
                }

                var arg = this.argumentKeysDictionary[key];
                if (arg.ArgumentType == typeof(bool))
                {
                    arg.Value = true;
                }
                else
                {
                    var valueIndex = ++i;
                    if (arguments.Length <= valueIndex)
                    {
                        throw new ApplicationException(string.Format("Value for {0} not specified", key));
                    }                 
                    arg.Value = Convert.ChangeType(arguments[valueIndex], arg.ArgumentType);
                }
            }

            foreach (var argument in argumentKeysDictionary)
            {
                if (argument.Value.IsRequired && !argument.Value.IsValueSet)
                {
                    throw new ApplicationException(string.Format("{0} value not set.", argument.Key));
                }
            }
        }

        public string GetHelpString()
        {
            var assembly = Assembly.GetEntryAssembly();
            var result = new StringBuilder();
            var assemblyName = assembly.GetName();
            result.AppendLine(string.Format("{0} v{1}", assemblyName.Name, assemblyName.Version));
            result.AppendFormat("Usage is: {0} [OPTION]\n\n", assembly.ManifestModule.Name);            

            var keysByArgument = new Dictionary<IArgument, List<string>>();
            foreach (var pair in argumentKeysDictionary)
            {
                var argument = pair.Value;
                if (!keysByArgument.ContainsKey(argument))
                {
                    keysByArgument.Add(argument, new List<string>());
                }
                keysByArgument[argument].Add(pair.Key);
            }

            foreach (var pair in keysByArgument)
            {
                var line = new StringBuilder();
                foreach (var key in pair.Value)
                {
                    line.AppendFormat("{0}, ", key);
                }

                int numberOfTabs = 2;
                if (line.Length > 15)
                {
                    numberOfTabs -= 1;
                }
                for (int i = 0; i < numberOfTabs; i++)
                {
                    line.Append("\t");   
                }                
                line.AppendFormat(" {0}", pair.Key.Description);
                result.AppendLine(line.ToString());
            }

            return result.ToString();
        }
    }

    public class Argument<T> : IArgument
    {
        public Argument()
        {
        }

        public Argument(string description)
        {
            this.Description = description;
        }

        public Argument(string description, bool required)
        {
            this.IsRequired = required;
            this.Description = description;
        }

        public string Description { get; set; }
        public bool IsRequired { get; set; }                    
        public T Value { get; set; }
        object IArgument.Value
        {
            set
            {
                this.Value = (T)value;
                IsValueSet = true;
            }
            get { return this.Value; }
        }

        public Type ArgumentType
        {
            get { return typeof (T); }
        }

        public bool IsValueSet { get; private set; }
    }

    public interface IArgument
    {
        string Description { get; }
        bool IsRequired { get; set; }
        object Value { get; set; }
        Type ArgumentType { get; }
        bool IsValueSet { get; }
    }
}
