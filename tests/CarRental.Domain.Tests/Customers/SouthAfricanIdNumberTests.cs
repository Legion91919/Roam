using CarRental.Domain.Customers;

namespace CarRental.Domain.Tests.Customers;

public class SouthAfricanIdNumberTests
{
    private static readonly DateOnly Today = new(2026, 11, 2);

    [Theory]
    [InlineData("9005105009088")]   // 10 May 1990
    [InlineData("0308125800083")]   // 12 Aug 2003
    [InlineData("8512310001082")]   // 31 Dec 1985
    [InlineData("0002295123083")]   // 29 Feb 2000 (leap year)
    public void Valid_numbers_pass(string number)
    {
        Assert.True(SouthAfricanIdNumber.IsValid(number, Today));
    }

    [Theory]
    [InlineData("9005105009089", "checksum")]        // last digit changed
    [InlineData("900510500908", "13 digits")]        // too short
    [InlineData("90051050090A8", "13 digits")]       // letter
    [InlineData("9013105009087", "date of birth")]   // month 13
    [InlineData("0102295123085", "date of birth")]   // 29 Feb 2001 does not exist
    [InlineData("9005105009288", "citizenship")]     // citizenship digit 2
    public void Invalid_numbers_are_rejected_with_a_reason(string number, string reason)
    {
        Assert.Contains(reason, SouthAfricanIdNumber.Validate(number, Today));
    }

    [Theory]
    [InlineData("9005105009088", 1990, 5, 10)]
    [InlineData("0308125800083", 2003, 8, 12)]
    public void The_century_is_inferred_so_the_birthday_is_in_the_past(string number, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), SouthAfricanIdNumber.DateOfBirth(number, Today));
    }
}
