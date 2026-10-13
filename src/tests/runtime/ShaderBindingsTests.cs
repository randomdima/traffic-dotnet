using TrafficSimulation.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Runtime;

/// <summary>
/// <b>A kernel's bindings are read off its own SPIR-V</b> (<see cref="Vk.BindingsDeclared"/>), so one bound with fewer
/// buffers than it declares is refused where it is made rather than read through a descriptor that is not there.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class ShaderBindingsTests
{
    /// <summary><c>integrate.comp</c> declares bindings 0 to 9, the last the bodies whose rest is counted afresh.</summary>
    [Fact]
    public void AKernelDeclaresOnePastItsHighestBinding()
    {
        using var stream = typeof(Vk).Assembly.GetManifestResourceStream("Shaders/integrate.comp.spv")!;
        var spirv = new byte[stream.Length];
        stream.ReadExactly(spirv);

        Assert.Equal(10, Vk.BindingsDeclared(spirv));
    }
}
