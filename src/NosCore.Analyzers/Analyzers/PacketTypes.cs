using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    internal static class PacketTypes
    {
        public static bool IsPacket(ITypeSymbol? type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (current.Name == "PacketBase" && current.ContainingNamespace?.ToDisplayString().StartsWith("NosCore.Packets") == true)
                {
                    return true;
                }
            }

            return false;
        }

        public static IPropertySymbol? PacketPropertyTarget(ISimpleAssignmentOperation assignment)
        {
            if (assignment.Target is not IPropertyReferenceOperation reference)
            {
                return null;
            }

            var type = reference.Instance?.Type ?? reference.Property.ContainingType;
            return IsPacket(type) || IsPacket(reference.Property.ContainingType) ? reference.Property : null;
        }

        public static IOperation Unwrap(IOperation operation)
        {
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            return operation;
        }
    }
}
