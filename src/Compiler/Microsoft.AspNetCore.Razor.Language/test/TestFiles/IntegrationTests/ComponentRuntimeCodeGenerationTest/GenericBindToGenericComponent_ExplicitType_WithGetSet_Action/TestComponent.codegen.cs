﻿// <auto-generated/>
#pragma warning disable 1591
namespace Test
{
    #line default
    using global::System;
    using global::System.Collections.Generic;
    using global::System.Linq;
    using global::System.Threading.Tasks;
    using global::Microsoft.AspNetCore.Components;
    #line default
    #line hidden
    #nullable restore
    public partial class TestComponent<
#nullable restore
#line (1,12)-(1,18) "x:\dir\subdir\Test\TestComponent.cshtml"
TParam

#line default
#line hidden
#nullable disable
    > : global::Microsoft.AspNetCore.Components.ComponentBase
    #nullable disable
    {
        #pragma warning disable 1998
        protected override void BuildRenderTree(global::Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder __builder)
        {
            __builder.OpenComponent<global::Test.MyComponent<TParam>>(0);
            __builder.AddComponentParameter(1, nameof(global::Test.MyComponent<TParam>.
#nullable restore
#line (2,36)-(2,41) "x:\dir\subdir\Test\TestComponent.cshtml"
Value

#line default
#line hidden
#nullable disable
            ), global::Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<TParam>(
#nullable restore
#line (2,47)-(2,58) "x:\dir\subdir\Test\TestComponent.cshtml"
ParentValue

#line default
#line hidden
#nullable disable
            ));
            __builder.AddComponentParameter(2, nameof(global::Test.MyComponent<TParam>.ValueChanged), global::Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.TypeCheck<global::Microsoft.AspNetCore.Components.EventCallback<TParam>>(global::Microsoft.AspNetCore.Components.EventCallback.Factory.Create<TParam>(this, global::Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers.CreateInferredEventCallback(this, 
#nullable restore
#line (2,77)-(2,88) "x:\dir\subdir\Test\TestComponent.cshtml"
UpdateValue

#line default
#line hidden
#nullable disable
            , ParentValue))));
            __builder.CloseComponent();
        }
        #pragma warning restore 1998
#nullable restore
#line (3,8)-(7,1) "x:\dir\subdir\Test\TestComponent.cshtml"

    public TParam ParentValue { get; set; } = default;

    public void UpdateValue(TParam value) { ParentValue = value; }

#line default
#line hidden
#nullable disable

    }
}
#pragma warning restore 1591
