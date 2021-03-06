﻿using Impatient.EFCore.Tests.Utilities;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace Impatient.EFCore.Tests
{
    public class DataAnnotationImpatientFixture : DataAnnotationImpatientTest.DataAnnotationFixtureBase
    {
        protected override ITestStoreFactory TestStoreFactory => ImpatientTestStoreFactory.Instance;
    }
}
